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
/// End-to-end tests for the reconciliation capability against the real API + PostgreSQL. Covers the
/// full flow (import → auto-match → divergence → justify → approve/reject), manual matching, the
/// batch-rejection cascade with entry reversal, and the RBAC denials (Auditor blocked on every write,
/// Editor blocked on approve/reject, cross-subsidiary writers blocked) — tasks 6.3–6.7.
/// </summary>
public sealed class ReconciliationEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public ReconciliationEndpointsTests(AuthApiFactory factory)
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

    private async Task<Guid> CategoryIdAsync()
    {
        var id = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            id = (await db.Categories.OrderBy(c => c.Code).FirstAsync()).Id;
        });
        return id;
    }

    /// <summary>Seeds an <c>Open</c> ledger entry directly in the DB and returns its id.</summary>
    private async Task<Guid> SeedOpenEntryAsync(Guid subsidiaryId, decimal amount, DateOnly date, string description = "Entry")
    {
        var categoryId = await CategoryIdAsync();
        var id = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            var entry = LedgerEntry.Create(subsidiaryId, categoryId, LedgerEntryType.Credit, amount, date, description, Guid.NewGuid());
            db.LedgerEntries.Add(entry);
            await db.SaveChangesAsync();
            id = entry.Id;
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

    private async Task<LedgerEntryStatus> EntryStatusAsync(Guid id)
    {
        var status = LedgerEntryStatus.Open;
        await _factory.WithDbContextAsync(async db =>
        {
            status = (await db.LedgerEntries.FirstAsync(e => e.Id == id)).Status;
        });
        return status;
    }

    // --- 6.3 happy path: import → unambiguous auto-match, and no-match → PendingReconciliation ---

    [Fact]
    public async Task Import_auto_matches_an_unambiguous_open_entry()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        // Line amount is exact and date is within the ±3-day window.
        var resp = await ImportCsvAsync(manager, sub, SampleCsv("2026-01-11,100.00,Credit,Deposit,"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        Assert.Equal(LedgerEntryStatus.Reconciled, await EntryStatusAsync(entryId));
        await _factory.WithDbContextAsync(async db =>
        {
            var line = await db.BankStatementLines.FirstAsync(l => l.SubsidiaryId == sub);
            Assert.Equal(BankStatementLineStatus.AutoMatched, line.Status);
            Assert.Equal(entryId, line.MatchedLedgerEntryId);
        });
    }

    [Fact]
    public async Task Import_with_no_matching_line_flags_the_entry_pending_reconciliation()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        // A line the entry cannot match (different amount) leaves the line unmatched and flags the entry.
        var resp = await ImportCsvAsync(manager, sub, SampleCsv("2026-01-11,999.00,Credit,Unrelated,"));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        Assert.Equal(LedgerEntryStatus.PendingReconciliation, await EntryStatusAsync(entryId));
        await _factory.WithDbContextAsync(async db =>
        {
            var line = await db.BankStatementLines.FirstAsync(l => l.SubsidiaryId == sub);
            Assert.Equal(BankStatementLineStatus.Unmatched, line.Status);
        });
    }

    // --- 6.4 divergence flow: justify (empty rejected, valid → locked) → approve; and reject ---

    [Fact]
    public async Task Editor_justifies_then_manager_approves()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        // Flip the entry to PendingReconciliation via a non-matching import.
        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,999.00,Credit,Unrelated,"));
        Assert.Equal(LedgerEntryStatus.PendingReconciliation, await EntryStatusAsync(entryId));

        // Empty justification is rejected (validation).
        var empty = await editor.PostAsJsonAsync($"/api/reconciliation/{entryId}/justify", new { justificationText = "" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(LedgerEntryStatus.PendingReconciliation, await EntryStatusAsync(entryId));

        // Valid justification → PendingApproval, and the entry is locked from Editor edits.
        var justify = await editor.PostAsJsonAsync($"/api/reconciliation/{entryId}/justify", new { justificationText = "Timing difference." });
        Assert.Equal(HttpStatusCode.NoContent, justify.StatusCode);
        Assert.Equal(LedgerEntryStatus.PendingApproval, await EntryStatusAsync(entryId));

        // Manager approves with no reason → Reconciled.
        var approve = await manager.PostAsync($"/api/reconciliation/{entryId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal(LedgerEntryStatus.Reconciled, await EntryStatusAsync(entryId));

        await _factory.WithDbContextAsync(async db =>
        {
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == entryId && a.Action == AuditAction.JustificationSubmitted));
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == entryId && a.Action == AuditAction.Approved));
        });
    }

    [Fact]
    public async Task Manager_rejects_a_justification_only_with_a_reason()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,999.00,Credit,Unrelated,"));
        await editor.PostAsJsonAsync($"/api/reconciliation/{entryId}/justify", new { justificationText = "Please approve." });
        Assert.Equal(LedgerEntryStatus.PendingApproval, await EntryStatusAsync(entryId));

        // Reject without a reason → validation error, entry unchanged.
        var noReason = await manager.PostAsJsonAsync($"/api/reconciliation/{entryId}/reject", new { rejectionReason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(LedgerEntryStatus.PendingApproval, await EntryStatusAsync(entryId));

        // Reject with a reason → back to PendingReconciliation.
        var reject = await manager.PostAsJsonAsync($"/api/reconciliation/{entryId}/reject", new { rejectionReason = "Attach the invoice." });
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);
        Assert.Equal(LedgerEntryStatus.PendingReconciliation, await EntryStatusAsync(entryId));
    }

    // --- 6.5 manual match: Editor links a pending entry to an unmatched line → Reconciled directly ---

    [Fact]
    public async Task Editor_manually_matches_a_pending_entry_to_an_unmatched_line()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        // Import a non-matching line so the entry becomes pending and the line stays unmatched.
        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,250.00,Credit,Different,"));
        var lineId = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            lineId = (await db.BankStatementLines.FirstAsync(l => l.SubsidiaryId == sub)).Id;
        });

        var match = await editor.PostAsJsonAsync("/api/reconciliation/manual-match", new { ledgerEntryId = entryId, bankStatementLineId = lineId });
        Assert.Equal(HttpStatusCode.NoContent, match.StatusCode);

        Assert.Equal(LedgerEntryStatus.Reconciled, await EntryStatusAsync(entryId));
        await _factory.WithDbContextAsync(async db =>
        {
            var line = await db.BankStatementLines.FirstAsync(l => l.Id == lineId);
            Assert.Equal(BankStatementLineStatus.ManuallyMatched, line.Status);
            Assert.Equal(entryId, line.MatchedLedgerEntryId);
        });
    }

    [Fact]
    public async Task Manual_match_against_an_already_matched_line_is_rejected()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var matchedEntry = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));
        var otherEntry = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        // The import auto-matches exactly one of the two identical entries? No — two candidates is
        // ambiguous, so nothing auto-matches. Instead give the line a unique amount matching only one.
        // Simpler: import a line matching neither, then auto-match leaves the line unmatched — not useful.
        // So: match the line to matchedEntry manually first, then attempt to reuse it.
        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,555.00,Credit,Standalone,"));
        var lineId = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            lineId = (await db.BankStatementLines.FirstAsync(l => l.SubsidiaryId == sub)).Id;
        });

        var first = await editor.PostAsJsonAsync("/api/reconciliation/manual-match", new { ledgerEntryId = matchedEntry, bankStatementLineId = lineId });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // The line is now ManuallyMatched — a second match is a conflict.
        var second = await editor.PostAsJsonAsync("/api/reconciliation/manual-match", new { ledgerEntryId = otherEntry, bankStatementLineId = lineId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // --- 6.6 batch-rejection cascade: reconciled entries revert, no new justification ---

    [Fact]
    public async Task Rejecting_a_batch_reverts_entries_matched_through_it_without_new_justification()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        var import = await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,100.00,Credit,Deposit,"));
        var batchId = (await ReadJsonAsync(import)).GetProperty("batchId").GetGuid();
        Assert.Equal(LedgerEntryStatus.Reconciled, await EntryStatusAsync(entryId));

        var reject = await manager.PatchAsJsonAsync($"/api/bank-statement-imports/{batchId}/reject", new { rejectionReason = "Wrong period imported." });
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        // The entry reverts, the match link is broken, a Reverted audit row exists, and NO justification was set.
        await _factory.WithDbContextAsync(async db =>
        {
            var entry = await db.LedgerEntries.FirstAsync(e => e.Id == entryId);
            Assert.Equal(LedgerEntryStatus.PendingReconciliation, entry.Status);
            Assert.Null(entry.JustificationText);

            var line = await db.BankStatementLines.FirstAsync(l => l.ImportBatchId == batchId);
            Assert.Equal(BankStatementLineStatus.Invalidated, line.Status);
            Assert.Null(line.MatchedLedgerEntryId);

            Assert.Equal(1, await db.AuditLogs.CountAsync(a =>
                a.EntityId == entryId && a.EntityType == "LedgerEntry" && a.Action == AuditAction.Reverted));
            // No justification was requested from the Editor as part of the revert (§6 decision #4).
            Assert.Equal(0, await db.AuditLogs.CountAsync(a =>
                a.EntityId == entryId && a.Action == AuditAction.JustificationSubmitted));
        });
    }

    // --- 6.7 RBAC denials ---

    [Fact]
    public async Task Auditor_is_forbidden_from_every_reconciliation_write()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var auditor = await AuthenticatedClientAsync(UserRole.Auditor, sub);
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,250.00,Credit,Unmatched,"));
        var lineId = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            lineId = (await db.BankStatementLines.FirstAsync(l => l.SubsidiaryId == sub)).Id;
        });

        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync("/api/reconciliation/manual-match", new { ledgerEntryId = entryId, bankStatementLineId = lineId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync($"/api/reconciliation/{entryId}/justify", new { justificationText = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsync($"/api/reconciliation/{entryId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync($"/api/reconciliation/{entryId}/reject", new { rejectionReason = "x" })).StatusCode);

        // Reads remain open to an Auditor.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/reconciliation")).StatusCode);
    }

    [Fact]
    public async Task Editor_is_forbidden_from_approving_and_rejecting()
    {
        var sub = await CreateSubsidiaryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);
        var entryId = await SeedOpenEntryAsync(sub, 100.00m, new DateOnly(2026, 1, 10));

        await ImportCsvAsync(manager, sub, SampleCsv("2026-01-10,999.00,Credit,Unrelated,"));
        await editor.PostAsJsonAsync($"/api/reconciliation/{entryId}/justify", new { justificationText = "Timing." });
        Assert.Equal(LedgerEntryStatus.PendingApproval, await EntryStatusAsync(entryId));

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync($"/api/reconciliation/{entryId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await editor.PostAsJsonAsync($"/api/reconciliation/{entryId}/reject", new { rejectionReason = "x" })).StatusCode);
    }

    [Fact]
    public async Task Editor_cannot_reconcile_outside_their_own_subsidiary()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var editorA = await AuthenticatedClientAsync(UserRole.Editor, subA);
        var entryB = await SeedOpenEntryAsync(subB, 100.00m, new DateOnly(2026, 1, 10));

        var justify = await editorA.PostAsJsonAsync($"/api/reconciliation/{entryB}/justify", new { justificationText = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, justify.StatusCode);
    }
}
