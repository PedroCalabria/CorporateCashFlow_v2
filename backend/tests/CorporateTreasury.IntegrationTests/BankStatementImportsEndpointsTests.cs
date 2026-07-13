using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// End-to-end tests for the bank-statement-import capability against the real API + PostgreSQL. Focus
/// is the role/scope matrix (Editor own-subsidiary, Auditor read-only, reject Manager-only), the
/// import per-row feedback + persisted-scope duplicate detection (§1.4/§3.7), and the batch rejection
/// transition with its audit row (tasks 6.3–6.8).
/// </summary>
public sealed class BankStatementImportsEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public BankStatementImportsEndpointsTests(AuthApiFactory factory)
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

    private Task<HttpClient> GlobalManagerClientAsync() => AuthenticatedClientAsync(UserRole.Manager, null);

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

    private static string SampleCsv(params string[] dataRows) =>
        string.Join('\n', new[] { "Date,Amount,Type,Description,DocumentNumber" }.Concat(dataRows));

    private static async Task<HttpResponseMessage> ImportCsvAsync(HttpClient client, Guid subsidiaryId, string csv)
    {
        using var form = new MultipartFormDataContent { { new StringContent(subsidiaryId.ToString()), "subsidiaryId" } };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "statement.csv");
        return await client.PostAsync("/api/bank-statement-imports", form);
    }

    private async Task<int> LineCountAsync(Guid subsidiaryId)
    {
        var count = 0;
        await _factory.WithDbContextAsync(async db =>
        {
            count = await db.BankStatementLines.CountAsync(l => l.SubsidiaryId == subsidiaryId);
        });
        return count;
    }

    // --- 6.3 ---
    [Fact]
    public async Task Editor_imports_valid_statement_in_own_subsidiary_and_is_blocked_elsewhere()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, subA);

        var csv = SampleCsv(
            "2026-01-05,100.50,Credit,Deposit,DOC1",
            "2026-01-06,75.00,Debit,Withdrawal,");

        var ownResp = await ImportCsvAsync(editor, subA, csv);
        Assert.Equal(HttpStatusCode.Created, ownResp.StatusCode);
        var body = await ReadJsonAsync(ownResp);
        Assert.Equal(2, body.GetProperty("createdCount").GetInt32());
        Assert.Equal("Processed", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("errors").GetArrayLength());

        await _factory.WithDbContextAsync(async db =>
        {
            var lines = await db.BankStatementLines.Where(l => l.SubsidiaryId == subA).ToListAsync();
            Assert.Equal(2, lines.Count);
            Assert.All(lines, l => Assert.Equal(BankStatementLineStatus.Unmatched, l.Status));
        });

        var otherResp = await ImportCsvAsync(editor, subB, csv);
        Assert.Equal(HttpStatusCode.Forbidden, otherResp.StatusCode);
        Assert.Equal(0, await LineCountAsync(subB));
    }

    // --- 6.4 ---
    [Fact]
    public async Task Auditor_is_forbidden_from_importing_and_rejecting()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var auditor = await AuthenticatedClientAsync(UserRole.Auditor, sub);

        // A batch the auditor will try to reject.
        var created = await ImportCsvAsync(manager, sub, SampleCsv("2026-01-05,10.00,Credit,Seed,"));
        var batchId = (await ReadJsonAsync(created)).GetProperty("batchId").GetGuid();

        var import = await ImportCsvAsync(auditor, sub, SampleCsv("2026-01-07,20.00,Credit,Nope,"));
        Assert.Equal(HttpStatusCode.Forbidden, import.StatusCode);

        var reject = await auditor.PatchAsJsonAsync($"/api/bank-statement-imports/{batchId}/reject", new { rejectionReason = "no" });
        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);

        // Read is allowed for an Auditor.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/bank-statement-imports")).StatusCode);
    }

    // --- 6.5 (a row matching an existing line is rejected while the rest import) ---
    [Fact]
    public async Task Import_row_matching_an_existing_line_is_rejected_while_other_rows_import()
    {
        var sub = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        // Seed one line via a first import.
        var seed = await ImportCsvAsync(editor, sub, SampleCsv("2026-03-01,200.00,Credit,Collides,"));
        Assert.Equal(HttpStatusCode.Created, seed.StatusCode);

        // Second file: one row duplicates the seeded line, one is new.
        var csv = SampleCsv(
            "2026-03-01,200.00,Credit,Collides,",   // data row 1: duplicate
            "2026-03-02,300.00,Debit,Fresh row,");   // data row 2: new
        var resp = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await ReadJsonAsync(resp);

        Assert.Equal(1, body.GetProperty("createdCount").GetInt32());
        Assert.Equal("ProcessedWithErrors", body.GetProperty("status").GetString());
        var error = body.GetProperty("errors").EnumerateArray().Single();
        Assert.Equal(1, error.GetProperty("rowNumber").GetInt32());
        Assert.Equal("Duplicate of an existing bank statement line", error.GetProperty("message").GetString());

        Assert.Equal(2, await LineCountAsync(sub)); // the seeded line + the one fresh row
    }

    // --- 6.6 (re-importing the exact same file creates no new lines) ---
    [Fact]
    public async Task Reimporting_the_same_file_creates_no_new_lines()
    {
        var sub = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var csv = SampleCsv(
            "2026-02-01,100.00,Credit,First row,",
            "2026-02-02,50.00,Debit,Second row,");

        var first = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(2, (await ReadJsonAsync(first)).GetProperty("createdCount").GetInt32());

        var second = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondBody = await ReadJsonAsync(second);

        Assert.Equal(0, secondBody.GetProperty("createdCount").GetInt32());
        Assert.Equal("ProcessedWithErrors", secondBody.GetProperty("status").GetString());
        var errors = secondBody.GetProperty("errors");
        Assert.Equal(2, errors.GetArrayLength());
        Assert.Equal(new HashSet<int> { 1, 2 }, errors.EnumerateArray().Select(e => e.GetProperty("rowNumber").GetInt32()).ToHashSet());
        Assert.All(errors.EnumerateArray(), e => Assert.Equal("Duplicate of an existing bank statement line", e.GetProperty("message").GetString()));

        Assert.Equal(2, await LineCountAsync(sub)); // still just the two from the first import
    }

    // --- 6.7 (Manager rejection: reason mandatory, cascade to Invalidated, audit row) ---
    [Fact]
    public async Task Reject_requires_a_reason_invalidates_lines_and_audits_and_is_manager_only()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var created = await ImportCsvAsync(manager, sub, SampleCsv(
            "2026-04-01,100.00,Credit,One,",
            "2026-04-02,200.00,Debit,Two,"));
        var batchId = (await ReadJsonAsync(created)).GetProperty("batchId").GetGuid();

        // Editor cannot reject.
        var editorReject = await editor.PatchAsJsonAsync($"/api/bank-statement-imports/{batchId}/reject", new { rejectionReason = "nope" });
        Assert.Equal(HttpStatusCode.Forbidden, editorReject.StatusCode);

        // Manager reject without a reason is rejected (400 validation).
        var noReason = await manager.PatchAsJsonAsync($"/api/bank-statement-imports/{batchId}/reject", new { rejectionReason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        // Manager reject with a reason succeeds.
        var withReason = await manager.PatchAsJsonAsync($"/api/bank-statement-imports/{batchId}/reject", new { rejectionReason = "wrong period" });
        Assert.Equal(HttpStatusCode.NoContent, withReason.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var batch = await db.BankStatementImportBatches.FirstAsync(b => b.Id == batchId);
            Assert.Equal(BankStatementBatchStatus.Rejected, batch.Status);
            Assert.Equal("wrong period", batch.RejectionReason);
            Assert.NotNull(batch.RejectedAt);
            Assert.NotNull(batch.RejectedBy);

            var lines = await db.BankStatementLines.Where(l => l.ImportBatchId == batchId).ToListAsync();
            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.Equal(BankStatementLineStatus.Invalidated, l.Status));

            var audit = await db.AuditLogs.CountAsync(a =>
                a.EntityId == batchId && a.EntityType == "BankStatementImportBatch" && a.Action == AuditAction.Rejected);
            Assert.Equal(1, audit);
        });
    }

    // --- 6.8 (scope isolation on listing) ---
    [Fact]
    public async Task Editor_sees_only_own_subsidiary_batches_while_global_manager_sees_all()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();

        await ImportCsvAsync(manager, subA, SampleCsv("2026-05-01,10.00,Credit,A,"));
        await ImportCsvAsync(manager, subB, SampleCsv("2026-05-02,20.00,Debit,B,"));

        var editorA = await AuthenticatedClientAsync(UserRole.Editor, subA);
        var list = await ReadJsonAsync(await editorA.GetAsync("/api/bank-statement-imports?pageSize=100"));
        foreach (var item in list.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(subA.ToString(), item.GetProperty("subsidiaryId").GetString());
        }

        // Global manager sees batches from both subsidiaries.
        var all = await ReadJsonAsync(await manager.GetAsync("/api/bank-statement-imports?pageSize=100"));
        var subsidiaries = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("subsidiaryId").GetString()).ToHashSet();
        Assert.Contains(subA.ToString(), subsidiaries);
        Assert.Contains(subB.ToString(), subsidiaries);
    }
}
