using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// End-to-end tests for the user-management capability against the real API + PostgreSQL. Focus is
/// the Global-vs-Subsidiary-Manager scope asymmetry (tasks 6.2–6.7), the reset-password session
/// kill (6.6), and the re-verification of the Subsidiary deactivation guard against real users (6.8).
/// </summary>
public sealed class UsersEndpointsTests : IClassFixture<AuthApiFactory>
{
    private const string RefreshCookieName = "refreshToken";

    private readonly AuthApiFactory _factory;

    public UsersEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>Seeds a user, logs in, and returns a client with the access token attached.</summary>
    private async Task<HttpClient> AuthenticatedClientAsync(UserRole role, Guid? subsidiaryId)
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!", role: role, subsidiaryId: subsidiaryId);
        return await LoginAsync(email, "Password123!");
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var token = (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> GlobalManagerClientAsync() => AuthenticatedClientAsync(UserRole.Manager, null);

    /// <summary>Creates a real subsidiary directly in the DB (FK target for scoped users) and returns its id.</summary>
    private async Task<Guid> CreateSubsidiaryAsync()
    {
        var id = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            var subsidiary = Subsidiary.Create("Sub", $"S-{Guid.NewGuid():N}"[..10], 0m, new DateOnly(2026, 1, 1));
            db.Subsidiaries.Add(subsidiary);
            await db.SaveChangesAsync();
            id = subsidiary.Id;
        });
        return id;
    }

    private static object CreateUserPayload(string role, Guid? subsidiaryId) => new
    {
        name = "New User",
        email = UniqueEmail(),
        role,
        subsidiaryId,
    };

    // --- 6.2 ---
    [Fact]
    public async Task Global_manager_creates_a_global_manager_with_a_one_time_password()
    {
        var client = await GlobalManagerClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Manager", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Manager", body.GetProperty("user").GetProperty("role").GetString());
        Assert.True(body.GetProperty("user").GetProperty("subsidiaryId").ValueKind == JsonValueKind.Null);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("initialPassword").GetString()));
    }

    // --- 6.3 ---
    [Fact]
    public async Task Subsidiary_manager_creation_scope_rules()
    {
        var subsidiaryA = await CreateSubsidiaryAsync();
        var subsidiaryB = await CreateSubsidiaryAsync();
        var client = await AuthenticatedClientAsync(UserRole.Manager, subsidiaryA);

        var otherSubsidiary = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiaryB));
        Assert.Equal(HttpStatusCode.Forbidden, otherSubsidiary.StatusCode);

        var makesManager = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Manager", subsidiaryA));
        Assert.Equal(HttpStatusCode.Forbidden, makesManager.StatusCode);

        var ownEditor = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiaryA));
        Assert.Equal(HttpStatusCode.Created, ownEditor.StatusCode);
    }

    // --- 6.4 ---
    [Fact]
    public async Task Subsidiary_manager_cannot_elevate_a_user_to_global_or_manager()
    {
        var subsidiaryA = await CreateSubsidiaryAsync();
        var globalClient = await GlobalManagerClientAsync();
        // A user in subsidiary A to be edited.
        var created = await globalClient.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiaryA));
        var userId = (await ReadJsonAsync(created)).GetProperty("user").GetProperty("id").GetGuid();

        var scopedClient = await AuthenticatedClientAsync(UserRole.Manager, subsidiaryA);

        var toGlobal = await scopedClient.PutAsJsonAsync($"/api/users/{userId}", new { role = "Auditor", subsidiaryId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Forbidden, toGlobal.StatusCode);

        var toManager = await scopedClient.PutAsJsonAsync($"/api/users/{userId}", new { role = "Manager", subsidiaryId = subsidiaryA });
        Assert.Equal(HttpStatusCode.Forbidden, toManager.StatusCode);
    }

    // --- 6.5 ---
    [Fact]
    public async Task Subsidiary_manager_lists_only_their_own_subsidiary_users()
    {
        var subsidiaryA = await CreateSubsidiaryAsync();
        var subsidiaryB = await CreateSubsidiaryAsync();
        var globalClient = await GlobalManagerClientAsync();
        await globalClient.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiaryA));
        await globalClient.PostAsJsonAsync("/api/users", CreateUserPayload("Auditor", subsidiaryB));

        var scopedClient = await AuthenticatedClientAsync(UserRole.Manager, subsidiaryA);
        var list = await scopedClient.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var users = await ReadJsonAsync(list);
        Assert.True(users.GetArrayLength() > 0);
        foreach (var user in users.EnumerateArray())
        {
            Assert.Equal(subsidiaryA.ToString(), user.GetProperty("subsidiaryId").GetString());
        }
    }

    // --- 6.6 ---
    [Fact]
    public async Task Reset_password_lets_user_log_in_with_new_password_and_kills_old_session()
    {
        var globalClient = await GlobalManagerClientAsync();
        var email = UniqueEmail();
        // Create the target user and capture its one-time password + id.
        var created = await globalClient.PostAsJsonAsync("/api/users", new
        {
            name = "Target",
            email,
            role = "Auditor",
            subsidiaryId = (Guid?)null,
        });
        var createdBody = await ReadJsonAsync(created);
        var userId = createdBody.GetProperty("user").GetProperty("id").GetGuid();
        var originalPassword = createdBody.GetProperty("initialPassword").GetString()!;

        // The target logs in and gets a refresh cookie (cookies handled off so we control it).
        var cookieClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var targetLogin = await cookieClient.PostAsJsonAsync("/api/auth/login", new { email, password = originalPassword });
        Assert.Equal(HttpStatusCode.OK, targetLogin.StatusCode);
        var oldRefresh = ExtractRefreshCookie(targetLogin)!;

        // Manager resets the password.
        var reset = await globalClient.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/users/{userId}/reset-password")
        {
            Content = JsonContent.Create(new { newPassword = "BrandNew123" }),
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // Old refresh token is now rejected (session killed).
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("Cookie", $"{RefreshCookieName}={oldRefresh}");
        var replayResult = await cookieClient.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResult.StatusCode);

        // New password works; old password does not.
        var newLogin = await cookieClient.PostAsJsonAsync("/api/auth/login", new { email, password = "BrandNew123" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var oldLogin = await cookieClient.PostAsJsonAsync("/api/auth/login", new { email, password = originalPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    // --- 6.7 ---
    [Theory]
    [InlineData(UserRole.Editor)]
    [InlineData(UserRole.Auditor)]
    public async Task Editors_and_auditors_are_forbidden_on_every_endpoint(UserRole role)
    {
        var subsidiary = await CreateSubsidiaryAsync();
        var subsidiaryId = role == UserRole.Editor ? subsidiary : (Guid?)null;
        var client = await AuthenticatedClientAsync(role, subsidiaryId);
        var someId = Guid.NewGuid();

        var get = await client.GetAsync("/api/users");
        var post = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiary));
        var put = await client.PutAsJsonAsync($"/api/users/{someId}", new { role = "Auditor", subsidiaryId = (Guid?)null });
        var deactivate = await client.PatchAsync($"/api/users/{someId}/deactivate", content: null);
        var reactivate = await client.PatchAsync($"/api/users/{someId}/reactivate", content: null);
        var reset = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/users/{someId}/reset-password")
        {
            Content = JsonContent.Create(new { newPassword = "BrandNew123" }),
        });

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
    }

    // --- 6.8 (Subsidiary deactivation guard verified against real users) ---
    [Fact]
    public async Task Subsidiary_with_a_real_active_user_cannot_be_deactivated_until_the_user_is_deactivated()
    {
        var client = await GlobalManagerClientAsync();

        // Create a subsidiary through the real endpoint, then an active Editor bound to it.
        var subCreate = await client.PostAsJsonAsync("/api/subsidiaries", new
        {
            name = "Guarded Co",
            code = $"G-{Guid.NewGuid():N}"[..10],
            initialBalance = 0m,
            referenceDate = "2026-01-01",
        });
        var subsidiaryId = (await ReadJsonAsync(subCreate)).GetProperty("id").GetGuid();

        var userCreate = await client.PostAsJsonAsync("/api/users", CreateUserPayload("Editor", subsidiaryId));
        var userId = (await ReadJsonAsync(userCreate)).GetProperty("user").GetProperty("id").GetGuid();

        // Blocked while the active user is assigned.
        var blocked = await client.PatchAsync($"/api/subsidiaries/{subsidiaryId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // Deactivate the user, then the subsidiary can be deactivated.
        var deactivateUser = await client.PatchAsync($"/api/users/{userId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, deactivateUser.StatusCode);

        var ok = await client.PatchAsync($"/api/subsidiaries/{subsidiaryId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False((await ReadJsonAsync(ok)).GetProperty("isActive").GetBoolean());
    }

    private static string? ExtractRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal))
            {
                continue;
            }

            var value = cookie.Split(';', 2)[0][$"{RefreshCookieName}=".Length..];
            return string.IsNullOrEmpty(value) ? null : value;
        }

        return null;
    }
}
