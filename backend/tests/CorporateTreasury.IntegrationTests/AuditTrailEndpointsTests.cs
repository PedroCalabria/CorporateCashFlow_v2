using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// End-to-end tests for the audit-trail capability against the real API + PostgreSQL: login
/// success/failure writing <c>AccessLog</c> rows, the global <c>AccessDenied</c> middleware
/// covering two distinct 403 mechanisms from two different capabilities, and the subsidiary-vs-
/// global read scoping of both <c>GET /api/audit-log</c> and <c>GET /api/access-log</c>.
/// </summary>
public sealed class AuditTrailEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuditTrailEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(UserRole role, Guid? subsidiaryId)
    {
        var email = UniqueEmail();
        await _factory.SeedUserAsync(email, "Password123!", role: role, subsidiaryId: subsidiaryId);
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        login.EnsureSuccessStatusCode();
        var token = (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> CurrentUserIdAsync(HttpClient client)
    {
        var me = await client.GetAsync("/api/auth/me");
        me.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(me)).GetProperty("id").GetGuid();
    }

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

    private async Task<Guid> AnyCategoryIdAsync()
    {
        var id = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            id = (await db.Categories.OrderBy(c => c.Code).FirstAsync()).Id;
        });
        return id;
    }

    private static object EntryPayload(Guid subsidiaryId, Guid categoryId) => new
    {
        subsidiaryId,
        categoryId,
        amount = 123.45m,
        date = "2026-01-15",
        description = "Audit trail test entry",
    };

    // --- 8.1 ---
    [Fact]
    public async Task Successful_login_writes_a_LoginSuccess_AccessLog_row()
    {
        var email = UniqueEmail();
        var user = await _factory.SeedUserAsync(email, "Password123!", role: UserRole.Manager);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var row = await db.AccessLogs.SingleAsync(a => a.UserId == user.Id && a.EventType == AccessLogEventType.LoginSuccess);
            Assert.NotNull(row);
        });
    }

    // --- 8.2 ---
    [Fact]
    public async Task Failed_logins_write_LoginFailed_rows_with_the_right_nullable_UserId_while_the_response_stays_generic()
    {
        var knownEmail = UniqueEmail();
        var knownUser = await _factory.SeedUserAsync(knownEmail, "Password123!");
        var unknownEmail = UniqueEmail();
        var client = _factory.CreateClient();

        var unknownResp = await client.PostAsJsonAsync("/api/auth/login", new { email = unknownEmail, password = "whatever" });
        var wrongPasswordResp = await client.PostAsJsonAsync("/api/auth/login", new { email = knownEmail, password = "WrongPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownResp.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResp.StatusCode);
        Assert.Equal(await unknownResp.Content.ReadAsStringAsync(), await wrongPasswordResp.Content.ReadAsStringAsync());

        await _factory.WithDbContextAsync(async db =>
        {
            var unknownRows = await db.AccessLogs
                .Where(a => a.EventType == AccessLogEventType.LoginFailed && a.UserId == null)
                .CountAsync();
            Assert.True(unknownRows >= 1);

            var knownRow = await db.AccessLogs
                .SingleAsync(a => a.UserId == knownUser.Id && a.EventType == AccessLogEventType.LoginFailed);
            Assert.NotNull(knownRow);
        });
    }

    // --- 8.3 ---
    [Fact]
    public async Task Two_different_403_mechanisms_from_two_different_capabilities_both_log_AccessDenied()
    {
        var subsidiaryId = await CreateSubsidiaryAsync();
        var scopedManager = await AuthenticatedClientAsync(UserRole.Manager, subsidiaryId);
        var editor = await AuthenticatedClientAsync(UserRole.Editor, subsidiaryId);

        // Policy-based 403: `subsidiaries` capability, GlobalManager policy — the authorization
        // middleware short-circuits without calling next(), so only a middleware registered
        // BEFORE it can observe the outcome (design.md §D3).
        var policyDenied = await scopedManager.GetAsync("/api/subsidiaries");
        Assert.Equal(HttpStatusCode.Forbidden, policyDenied.StatusCode);

        // Manual 403: `ledger-entries` capability, ForbiddenOperationException → GuardedAsync.
        // EnsureManager() runs before the entry is even loaded, so a nonexistent id still 403s.
        var manualDeleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { deletionReason = "n/a" }),
        };
        var manualDenied = await editor.SendAsync(manualDeleteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, manualDenied.StatusCode);

        var scopedManagerId = await CurrentUserIdAsync(scopedManager);
        var editorId = await CurrentUserIdAsync(editor);

        await _factory.WithDbContextAsync(async db =>
        {
            var policyRows = await db.AccessLogs
                .CountAsync(a => a.UserId == scopedManagerId && a.EventType == AccessLogEventType.AccessDenied);
            Assert.True(policyRows >= 1);

            var manualRows = await db.AccessLogs
                .CountAsync(a => a.UserId == editorId && a.EventType == AccessLogEventType.AccessDenied);
            Assert.True(manualRows >= 1);
        });
    }

    // --- 8.4 ---
    [Fact]
    public async Task Audit_log_is_scoped_by_subsidiary_and_global_sees_everything()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var categoryId = await AnyCategoryIdAsync();

        var editorA = await AuthenticatedClientAsync(UserRole.Editor, subA);
        var editorB = await AuthenticatedClientAsync(UserRole.Editor, subB);

        var createA = await editorA.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subA, categoryId));
        var createB = await editorB.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subB, categoryId));
        Assert.Equal(HttpStatusCode.Created, createA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createB.StatusCode);
        var entryAId = (await ReadJsonAsync(createA)).GetProperty("id").GetGuid();
        var entryBId = (await ReadJsonAsync(createB)).GetProperty("id").GetGuid();

        var scopedManagerA = await AuthenticatedClientAsync(UserRole.Manager, subA);
        var scopedResp = await scopedManagerA.GetAsync("/api/audit-log?pageSize=500");
        Assert.Equal(HttpStatusCode.OK, scopedResp.StatusCode);
        var scopedItems = (await ReadJsonAsync(scopedResp)).GetProperty("items").EnumerateArray().ToList();
        var scopedIds = scopedItems.Select(e => e.GetProperty("entityId").GetGuid()).ToList();
        Assert.Contains(entryAId, scopedIds);
        Assert.DoesNotContain(entryBId, scopedIds);

        // performedByName is resolved to the acting user's real name — never the raw Guid
        // (regression check for the display bug fixed after initial delivery).
        var entryARow = scopedItems.Single(e => e.GetProperty("entityId").GetGuid() == entryAId);
        Assert.Equal("Test User", entryARow.GetProperty("performedByName").GetString());

        var globalAuditor = await AuthenticatedClientAsync(UserRole.Auditor, null);
        var globalResp = await globalAuditor.GetAsync("/api/audit-log?pageSize=500");
        Assert.Equal(HttpStatusCode.OK, globalResp.StatusCode);
        var globalIds = (await ReadJsonAsync(globalResp)).GetProperty("items")
            .EnumerateArray().Select(e => e.GetProperty("entityId").GetGuid()).ToList();
        Assert.Contains(entryAId, globalIds);
        Assert.Contains(entryBId, globalIds);
    }

    // --- 8.5 ---
    [Fact]
    public async Task Access_log_is_scoped_by_subsidiary_excluding_unattributed_rows_and_global_sees_everything()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();

        var emailA = UniqueEmail();
        var userA = await _factory.SeedUserAsync(emailA, "Password123!", role: UserRole.Editor, subsidiaryId: subA);
        var emailB = UniqueEmail();
        var userB = await _factory.SeedUserAsync(emailB, "Password123!", role: UserRole.Editor, subsidiaryId: subB);

        var anonymousClient = _factory.CreateClient();
        await anonymousClient.PostAsJsonAsync("/api/auth/login", new { email = emailA, password = "Password123!" });
        await anonymousClient.PostAsJsonAsync("/api/auth/login", new { email = emailB, password = "Password123!" });
        // Unattributed failed login (unknown email) — UserId null, visible only to a global caller.
        await anonymousClient.PostAsJsonAsync("/api/auth/login", new { email = UniqueEmail(), password = "whatever" });

        var scopedManagerA = await AuthenticatedClientAsync(UserRole.Manager, subA);
        var scopedResp = await scopedManagerA.GetAsync("/api/access-log?pageSize=500");
        Assert.Equal(HttpStatusCode.OK, scopedResp.StatusCode);
        var scopedItems = (await ReadJsonAsync(scopedResp)).GetProperty("items").EnumerateArray().ToList();
        var scopedUserIds = scopedItems
            .Select(e => e.GetProperty("userId").ValueKind == JsonValueKind.Null ? (Guid?)null : e.GetProperty("userId").GetGuid())
            .ToList();
        Assert.Contains(userA.Id, scopedUserIds);
        Assert.DoesNotContain(userB.Id, scopedUserIds);
        Assert.DoesNotContain(null, scopedUserIds);

        // userName is resolved to the real name — never the raw Guid (regression check for the
        // display bug fixed after initial delivery).
        var userARow = scopedItems.Single(e => e.GetProperty("userId").GetGuid() == userA.Id);
        Assert.Equal("Test User", userARow.GetProperty("userName").GetString());

        var globalAuditor = await AuthenticatedClientAsync(UserRole.Auditor, null);
        var globalResp = await globalAuditor.GetAsync("/api/access-log?pageSize=500");
        Assert.Equal(HttpStatusCode.OK, globalResp.StatusCode);
        var globalItems = (await ReadJsonAsync(globalResp)).GetProperty("items").EnumerateArray().ToList();
        var globalUserIds = globalItems
            .Select(e => e.GetProperty("userId").ValueKind == JsonValueKind.Null ? (Guid?)null : e.GetProperty("userId").GetGuid())
            .ToList();
        Assert.Contains(userA.Id, globalUserIds);
        Assert.Contains(userB.Id, globalUserIds);
        Assert.Contains(null, globalUserIds);
    }

    // --- 8.6 ---
    [Fact]
    public async Task Editor_is_forbidden_from_both_audit_log_and_access_log()
    {
        var subsidiaryId = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, subsidiaryId);

        var auditResp = await editor.GetAsync("/api/audit-log");
        var accessResp = await editor.GetAsync("/api/access-log");

        Assert.Equal(HttpStatusCode.Forbidden, auditResp.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, accessResp.StatusCode);
    }
}
