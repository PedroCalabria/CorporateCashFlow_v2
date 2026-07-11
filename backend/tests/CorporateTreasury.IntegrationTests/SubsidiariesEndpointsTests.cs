using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// End-to-end tests for the subsidiaries capability against the real API + PostgreSQL. Covers the
/// Global-Manager-only RBAC on every method, the atomic create, baseline immutability on update,
/// and the deactivation integrity guard (tasks 6.2–6.6).
/// </summary>
public sealed class SubsidiariesEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public SubsidiariesEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string UniqueCode() => $"C-{Guid.NewGuid():N}"[..12];

    /// <summary>Seeds a user, logs in, and returns a client with the access token attached.</summary>
    private async Task<HttpClient> AuthenticatedClientAsync(UserRole role, Guid? subsidiaryId)
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!", role: role, subsidiaryId: subsidiaryId);

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        login.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private Task<HttpClient> GlobalManagerClientAsync() =>
        AuthenticatedClientAsync(UserRole.Manager, subsidiaryId: null);

    private static object NewSubsidiaryPayload(string code) => new
    {
        name = "Acme North",
        code,
        initialBalance = 1000.00m,
        referenceDate = "2026-01-01",
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    // --- 6.2 ---
    [Fact]
    public async Task Global_manager_creates_a_subsidiary_with_its_bank_account_active()
    {
        var client = await GlobalManagerClientAsync();

        var response = await client.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(1000.00m, body.GetProperty("initialBalance").GetDecimal());
        Assert.Equal("2026-01-01", body.GetProperty("referenceDate").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
    }

    // --- 6.3 ---
    [Fact]
    public async Task Subsidiary_scoped_user_gets_403_on_every_endpoint_and_method()
    {
        // The scoped user's SubsidiaryId is now a real FK, so it must reference an existing
        // subsidiary — create one directly in the DB and scope the user to it.
        var scopeSubsidiaryId = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            var subsidiary = Subsidiary.Create("Scoped Co", UniqueCode(), 0m, new DateOnly(2026, 1, 1));
            db.Subsidiaries.Add(subsidiary);
            await db.SaveChangesAsync();
            scopeSubsidiaryId = subsidiary.Id;
        });

        var scopedClient = await AuthenticatedClientAsync(UserRole.Manager, subsidiaryId: scopeSubsidiaryId);
        var someId = Guid.NewGuid();

        var get = await scopedClient.GetAsync("/api/subsidiaries");
        var post = await scopedClient.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));
        var put = await scopedClient.PutAsJsonAsync($"/api/subsidiaries/{someId}", new { name = "X", code = "Y" });
        var deactivate = await scopedClient.PatchAsync($"/api/subsidiaries/{someId}/deactivate", content: null);
        var reactivate = await scopedClient.PatchAsync($"/api/subsidiaries/{someId}/reactivate", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_gets_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/subsidiaries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- 6.4 ---
    [Fact]
    public async Task Update_ignores_initial_balance_and_reference_date_and_never_persists_them()
    {
        var client = await GlobalManagerClientAsync();
        var create = await client.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetGuid();

        // Send the immutable baseline fields in the update payload — they are not part of the
        // contract, so the model binder ignores them and they are never persisted.
        var update = await client.PutAsJsonAsync($"/api/subsidiaries/{id}", new
        {
            name = "Renamed",
            code = UniqueCode(),
            initialBalance = 999999.99m,
            referenceDate = "1990-12-31",
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await ReadJsonAsync(update);
        Assert.Equal("Renamed", body.GetProperty("name").GetString());
        Assert.Equal(1000.00m, body.GetProperty("initialBalance").GetDecimal()); // unchanged
        Assert.Equal("2026-01-01", body.GetProperty("referenceDate").GetString()); // unchanged
    }

    // --- 6.5 ---
    [Fact]
    public async Task Deactivation_is_blocked_while_an_active_user_is_assigned_but_succeeds_otherwise()
    {
        var client = await GlobalManagerClientAsync();

        // Blocked: create a subsidiary, assign an active user to it, then try to deactivate.
        var withUser = await client.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));
        var withUserId = (await ReadJsonAsync(withUser)).GetProperty("id").GetGuid();
        await _factory.SeedUserAsync(UniqueEmail(), "Password123!", role: UserRole.Editor, subsidiaryId: withUserId);

        var blocked = await client.PatchAsync($"/api/subsidiaries/{withUserId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedBody = await ReadJsonAsync(blocked);
        Assert.Equal("SUBSIDIARY_HAS_ACTIVE_USERS", blockedBody.GetProperty("code").GetString());

        // Still active after the blocked attempt.
        var afterBlock = await ReadJsonAsync(await client.PatchAsync($"/api/subsidiaries/{withUserId}/reactivate", content: null));
        Assert.True(afterBlock.GetProperty("isActive").GetBoolean());

        // Succeeds: a subsidiary with no active users deactivates cleanly.
        var noUser = await client.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));
        var noUserId = (await ReadJsonAsync(noUser)).GetProperty("id").GetGuid();

        var ok = await client.PatchAsync($"/api/subsidiaries/{noUserId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False((await ReadJsonAsync(ok)).GetProperty("isActive").GetBoolean());
    }

    // --- 6.6 ---
    [Fact]
    public async Task Deactivated_subsidiary_can_be_reactivated()
    {
        var client = await GlobalManagerClientAsync();
        var create = await client.PostAsJsonAsync("/api/subsidiaries", NewSubsidiaryPayload(UniqueCode()));
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetGuid();

        await client.PatchAsync($"/api/subsidiaries/{id}/deactivate", content: null);
        var reactivated = await client.PatchAsync($"/api/subsidiaries/{id}/reactivate", content: null);

        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await ReadJsonAsync(reactivated)).GetProperty("isActive").GetBoolean());
    }
}
