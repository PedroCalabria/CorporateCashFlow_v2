using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CorporateTreasury.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// End-to-end auth flow tests against the real API + PostgreSQL. Cookie handling is disabled
/// on the client so each test controls exactly which refresh cookie it presents — essential
/// for exercising rotation, revocation, and logout replay.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<AuthApiFactory>
{
    private const string RefreshCookieName = "refreshToken";

    private readonly AuthApiFactory _factory;

    public AuthEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Extract the refresh-token value from a response's Set-Cookie header (null if absent/cleared).</summary>
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

            var firstSegment = cookie.Split(';', 2)[0];
            var value = firstSegment[$"{RefreshCookieName}=".Length..];
            return string.IsNullOrEmpty(value) ? null : value;
        }

        return null;
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage PostWithRefreshCookie(string path, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        return request;
    }

    // --- 6.2 ---
    [Fact]
    public async Task Login_with_valid_credentials_returns_access_token_and_sets_refresh_cookie()
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!", role: UserRole.Manager);
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await ReadAccessTokenAsync(response)));
        Assert.NotNull(ExtractRefreshCookie(response));
    }

    // --- 6.3 ---
    [Fact]
    public async Task Unknown_email_wrong_password_and_inactive_user_all_return_the_same_generic_401()
    {
        var activeEmail = UniqueEmail();
        await _factory.SeedUserAsync(activeEmail, "Password123!");
        var inactiveEmail = UniqueEmail();
        await _factory.SeedUserAsync(inactiveEmail, "Password123!", isActive: false);
        var client = CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { email = UniqueEmail(), password = "Password123!" });
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { email = activeEmail, password = "WrongPassword!" });
        var inactive = await client.PostAsJsonAsync("/api/auth/login",
            new { email = inactiveEmail, password = "Password123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);

        // No user enumeration: identical body, and no cookie issued on any failure.
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();
        var inactiveBody = await inactive.Content.ReadAsStringAsync();
        Assert.Equal(unknownBody, wrongBody);
        Assert.Equal(unknownBody, inactiveBody);
        Assert.Null(ExtractRefreshCookie(unknown));
        Assert.Null(ExtractRefreshCookie(wrongPassword));
        Assert.Null(ExtractRefreshCookie(inactive));
    }

    // --- 6.4 ---
    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_is_then_rejected()
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!");
        var client = CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var oldRefresh = ExtractRefreshCookie(login)!;

        // First refresh with the valid cookie → new access token + new cookie (rotation).
        var refresh = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", oldRefresh));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.False(string.IsNullOrEmpty(await ReadAccessTokenAsync(refresh)));
        var newRefresh = ExtractRefreshCookie(refresh)!;
        Assert.NotEqual(oldRefresh, newRefresh);

        // Replaying the OLD (now revoked) cookie is rejected.
        var replay = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", oldRefresh));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The freshly rotated cookie still works.
        var followUp = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", newRefresh));
        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
    }

    // --- 6.5 ---
    [Fact]
    public async Task Expired_or_revoked_refresh_token_returns_401()
    {
        // Expired.
        var expiredEmail = UniqueEmail();
        var expiredUser = await _factory.SeedUserAsync(expiredEmail, "Password123!");
        var client = CreateClient();

        var expiredLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { email = expiredEmail, password = "Password123!" });
        var expiredCookie = ExtractRefreshCookie(expiredLogin)!;
        await _factory.WithDbContextAsync(async db =>
        {
            var token = db.RefreshTokens.OrderByDescending(t => t.CreatedAt)
                .First(t => t.UserId == expiredUser.Id);
            token.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });
        var expiredResult = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", expiredCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResult.StatusCode);

        // Revoked.
        var revokedEmail = UniqueEmail();
        var revokedUser = await _factory.SeedUserAsync(revokedEmail, "Password123!");
        var revokedLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { email = revokedEmail, password = "Password123!" });
        var revokedCookie = ExtractRefreshCookie(revokedLogin)!;
        await _factory.WithDbContextAsync(async db =>
        {
            var token = db.RefreshTokens.OrderByDescending(t => t.CreatedAt)
                .First(t => t.UserId == revokedUser.Id);
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var revokedResult = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", revokedCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedResult.StatusCode);
    }

    // --- 6.6 ---
    [Fact]
    public async Task After_logout_the_same_refresh_token_is_rejected()
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!");
        var client = CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var accessToken = await ReadAccessTokenAsync(login);
        var refreshCookie = ExtractRefreshCookie(login)!;

        // Logout requires the access token (authenticated) and presents the refresh cookie.
        var logout = PostWithRefreshCookie("/api/auth/logout", refreshCookie);
        logout.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResult = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResult.StatusCode);

        // The revoked token can no longer refresh.
        var refresh = await client.SendAsync(PostWithRefreshCookie("/api/auth/refresh", refreshCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
