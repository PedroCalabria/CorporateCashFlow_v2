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
/// End-to-end tests for the ledger-entries capability against the real API + PostgreSQL. Focus is
/// the role/scope matrix (Editor own-subsidiary, Manager-direct, Auditor read-only, delete
/// Manager-only), the import per-row feedback, automatic audit persistence, and the completed
/// subsidiary deactivation guard (tasks 6.3–6.9).
/// </summary>
public sealed class LedgerEntriesEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public LedgerEntriesEndpointsTests(AuthApiFactory factory)
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

    private async Task<(Guid Id, string Code)> AnyCategoryAsync()
    {
        Guid id = Guid.Empty;
        string code = "";
        await _factory.WithDbContextAsync(async db =>
        {
            var c = await db.Categories.OrderBy(x => x.Code).FirstAsync();
            id = c.Id;
            code = c.Code;
        });
        return (id, code);
    }

    private static object EntryPayload(Guid subsidiaryId, Guid categoryId) => new
    {
        subsidiaryId,
        categoryId,
        amount = 123.45m,
        date = "2026-01-15",
        description = "Test entry",
    };

    private static async Task<int> AuditCountAsync(AuthApiFactory factory, Guid entryId, AuditAction action)
    {
        var count = 0;
        await factory.WithDbContextAsync(async db =>
        {
            count = await db.AuditLogs.CountAsync(a => a.EntityId == entryId && a.Action == action);
        });
        return count;
    }

    private async Task<int> EntryCountAsync(Guid subsidiaryId)
    {
        var count = 0;
        await _factory.WithDbContextAsync(async db =>
        {
            count = await db.LedgerEntries.CountAsync(e => e.SubsidiaryId == subsidiaryId);
        });
        return count;
    }

    private static async Task<HttpResponseMessage> ImportCsvAsync(HttpClient client, Guid subsidiaryId, string csv)
    {
        using var form = new MultipartFormDataContent { { new StringContent(subsidiaryId.ToString()), "subsidiaryId" } };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "entries.csv");
        return await client.PostAsync("/api/ledger-entries/import", form);
    }

    // --- 6.3 ---
    [Fact]
    public async Task Editor_creates_in_own_subsidiary_and_is_blocked_elsewhere()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, subA);

        var ownResp = await editor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subA, categoryId));
        Assert.Equal(HttpStatusCode.Created, ownResp.StatusCode);
        var body = await ReadJsonAsync(ownResp);
        Assert.Equal("Open", body.GetProperty("status").GetString());
        var entryId = body.GetProperty("id").GetGuid();
        Assert.Equal(1, await AuditCountAsync(_factory, entryId, AuditAction.Created));

        var otherResp = await editor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subB, categoryId));
        Assert.Equal(HttpStatusCode.Forbidden, otherResp.StatusCode);
    }

    // --- 6.4 ---
    [Fact]
    public async Task Import_creates_valid_rows_and_reports_invalid_ones()
    {
        var sub = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var csv = string.Join('\n',
            "Date,CategoryCode,Amount,Description",
            "2026-01-05,SALES_REVENUE,100.50,First valid",
            "2026-13-40,SALES_REVENUE,50,Invalid date",
            "2026-01-06,NOPE,10,Unknown category",
            "2026-01-07,PAYROLL,-5,Bad amount",
            "2026-01-08,PAYROLL,75,Second valid");

        using var form = new MultipartFormDataContent { { new StringContent(sub.ToString()), "subsidiaryId" } };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "entries.csv");

        var resp = await editor.PostAsync("/api/ledger-entries/import", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await ReadJsonAsync(resp);

        Assert.Equal(2, body.GetProperty("createdCount").GetInt32());
        var errors = body.GetProperty("errors");
        Assert.Equal(3, errors.GetArrayLength());
        var reportedRows = errors.EnumerateArray().Select(e => e.GetProperty("rowNumber").GetInt32()).ToHashSet();
        Assert.Equal(new HashSet<int> { 2, 3, 4 }, reportedRows);
    }

    // --- 6.4 (XLSX) ---
    [Fact]
    public async Task Import_accepts_xlsx_with_native_date_and_number_cells()
    {
        var sub = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        // Rows mirror the CSV case: 2 valid, 3 invalid (bad date, unknown category, non-positive amount).
        // Valid rows use real Excel date/number cells to prove locale-independent normalization.
        var xlsx = BuildXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "CategoryCode";
            ws.Cell(1, 3).Value = "Amount";
            ws.Cell(1, 4).Value = "Description";

            ws.Cell(2, 1).Value = new DateTime(2026, 1, 5); // native date cell
            ws.Cell(2, 2).Value = "SALES_REVENUE";
            ws.Cell(2, 3).Value = 100.50; // native number cell
            ws.Cell(2, 4).Value = "First valid";

            ws.Cell(3, 1).Value = "2026-13-40"; // text: invalid date
            ws.Cell(3, 2).Value = "SALES_REVENUE";
            ws.Cell(3, 3).Value = 50;
            ws.Cell(3, 4).Value = "Invalid date";

            ws.Cell(4, 1).Value = new DateTime(2026, 1, 6);
            ws.Cell(4, 2).Value = "NOPE"; // unknown category
            ws.Cell(4, 3).Value = 10;
            ws.Cell(4, 4).Value = "Unknown category";

            ws.Cell(5, 1).Value = new DateTime(2026, 1, 7);
            ws.Cell(5, 2).Value = "PAYROLL";
            ws.Cell(5, 3).Value = -5; // non-positive amount
            ws.Cell(5, 4).Value = "Bad amount";

            ws.Cell(6, 1).Value = new DateTime(2026, 1, 8);
            ws.Cell(6, 2).Value = "PAYROLL";
            ws.Cell(6, 3).Value = 75;
            ws.Cell(6, 4).Value = "Second valid";
        });

        using var form = new MultipartFormDataContent { { new StringContent(sub.ToString()), "subsidiaryId" } };
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "entries.xlsx");

        var resp = await editor.PostAsync("/api/ledger-entries/import", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await ReadJsonAsync(resp);

        Assert.Equal(2, body.GetProperty("createdCount").GetInt32());
        var errors = body.GetProperty("errors");
        Assert.Equal(3, errors.GetArrayLength());
        var reportedRows = errors.EnumerateArray().Select(e => e.GetProperty("rowNumber").GetInt32()).ToHashSet();
        Assert.Equal(new HashSet<int> { 2, 3, 4 }, reportedRows);
    }

    private static byte[] BuildXlsx(Action<ClosedXML.Excel.IXLWorksheet> fill)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        fill(workbook.Worksheets.Add("Entries"));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // --- 8.4 (§1.4 duplicate detection: re-importing the same file creates no duplicates) ---
    [Fact]
    public async Task Reimporting_the_same_file_creates_no_duplicate_entries()
    {
        var sub = await CreateSubsidiaryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var csv = string.Join('\n',
            "Date,CategoryCode,Amount,Description",
            "2026-02-01,SALES_REVENUE,100.00,First row",
            "2026-02-02,PAYROLL,50.00,Second row");

        // First import: both rows are new and created.
        var first = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadJsonAsync(first);
        Assert.Equal(2, firstBody.GetProperty("createdCount").GetInt32());
        Assert.Equal(0, firstBody.GetProperty("errors").GetArrayLength());

        // Same file again: nothing is created; every row is reported as a duplicate.
        var second = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await ReadJsonAsync(second);

        Assert.Equal(0, secondBody.GetProperty("createdCount").GetInt32());
        var errors = secondBody.GetProperty("errors");
        Assert.Equal(2, errors.GetArrayLength());
        Assert.Equal(new HashSet<int> { 1, 2 }, errors.EnumerateArray().Select(e => e.GetProperty("rowNumber").GetInt32()).ToHashSet());
        Assert.All(errors.EnumerateArray(), e => Assert.Equal("Duplicate of an existing entry", e.GetProperty("message").GetString()));

        // No duplicate was persisted — still exactly the two entries from the first import.
        Assert.Equal(2, await EntryCountAsync(sub));
    }

    // --- 8.4 (§1.4: a row matching a manual entry is rejected while the file's other rows import) ---
    [Fact]
    public async Task Import_row_matching_a_manual_entry_is_rejected_while_other_rows_import()
    {
        var sub = await CreateSubsidiaryAsync();
        var (categoryId, categoryCode) = await AnyCategoryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        // A manually-created entry the import must recognize as a duplicate.
        var manual = await editor.PostAsJsonAsync("/api/ledger-entries", new
        {
            subsidiaryId = sub,
            categoryId,
            amount = 200.00m,
            date = "2026-03-01",
            description = "Collides",
        });
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);

        var csv = string.Join('\n',
            "Date,CategoryCode,Amount,Description",
            $"2026-03-01,{categoryCode},200.00,Collides", // data row 1: duplicates the manual entry
            $"2026-03-02,{categoryCode},300.00,Fresh row"); // data row 2: new

        var resp = await ImportCsvAsync(editor, sub, csv);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await ReadJsonAsync(resp);

        Assert.Equal(1, body.GetProperty("createdCount").GetInt32());
        var error = body.GetProperty("errors").EnumerateArray().Single();
        Assert.Equal(1, error.GetProperty("rowNumber").GetInt32());
        Assert.Equal("Duplicate of an existing entry", error.GetProperty("message").GetString());

        Assert.Equal(2, await EntryCountAsync(sub)); // the manual entry + the one fresh row
    }

    // --- 8.4 (§1.4: manual creation is NOT subject to the duplicate rule) ---
    [Fact]
    public async Task Manual_creation_allows_two_identical_entries()
    {
        var sub = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var first = await editor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(sub, categoryId));
        var second = await editor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(sub, categoryId));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(2, await EntryCountAsync(sub));
    }

    // --- 6.5 ---
    [Fact]
    public async Task Open_entries_are_editable_by_editor_and_manager_but_not_when_non_open()
    {
        var sub = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);
        var manager = await GlobalManagerClientAsync();

        var created = await editor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(sub, categoryId));
        var entryId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var editByEditor = await editor.PutAsJsonAsync($"/api/ledger-entries/{entryId}",
            new { categoryId, amount = 200m, date = "2026-01-20", description = "Edited by editor" });
        Assert.Equal(HttpStatusCode.OK, editByEditor.StatusCode);
        Assert.Equal(1, await AuditCountAsync(_factory, entryId, AuditAction.Updated));

        var editByManager = await manager.PutAsJsonAsync($"/api/ledger-entries/{entryId}",
            new { categoryId, amount = 300m, date = "2026-01-21", description = "Edited by manager directly" });
        Assert.Equal(HttpStatusCode.OK, editByManager.StatusCode);

        // Make it non-Open (Manager soft-delete), then editing must be rejected.
        var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{entryId}")
        {
            Content = JsonContent.Create(new { deletionReason = "closing" }),
        };
        Assert.Equal(HttpStatusCode.NoContent, (await manager.SendAsync(del)).StatusCode);

        var editDeleted = await manager.PutAsJsonAsync($"/api/ledger-entries/{entryId}",
            new { categoryId, amount = 1m, date = "2026-01-22", description = "should fail" });
        Assert.Equal(HttpStatusCode.Conflict, editDeleted.StatusCode);
    }

    // --- 6.6 ---
    [Fact]
    public async Task Auditor_is_forbidden_from_writing_on_every_endpoint()
    {
        var sub = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var auditor = await AuthenticatedClientAsync(UserRole.Auditor, sub);
        var someId = Guid.NewGuid();

        var post = await auditor.PostAsJsonAsync("/api/ledger-entries", EntryPayload(sub, categoryId));
        var put = await auditor.PutAsJsonAsync($"/api/ledger-entries/{someId}",
            new { categoryId, amount = 1m, date = "2026-01-01", description = "x" });
        var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{someId}")
        {
            Content = JsonContent.Create(new { deletionReason = "x" }),
        };
        var delResp = await auditor.SendAsync(del);

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delResp.StatusCode);

        // Read is allowed for an Auditor.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/ledger-entries")).StatusCode);
    }

    // --- 6.7 ---
    [Fact]
    public async Task Delete_requires_a_reason_and_is_manager_only()
    {
        var sub = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var manager = await GlobalManagerClientAsync();
        var editor = await AuthenticatedClientAsync(UserRole.Editor, sub);

        var created = await manager.PostAsJsonAsync("/api/ledger-entries", EntryPayload(sub, categoryId));
        var entryId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        // Editor cannot delete.
        var editorDel = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{entryId}")
        {
            Content = JsonContent.Create(new { deletionReason = "nope" }),
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.SendAsync(editorDel)).StatusCode);

        // Manager delete without a reason is rejected.
        var noReason = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{entryId}")
        {
            Content = JsonContent.Create(new { deletionReason = "" }),
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.SendAsync(noReason)).StatusCode);

        // Manager delete with a reason soft-deletes and audits it.
        var withReason = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{entryId}")
        {
            Content = JsonContent.Create(new { deletionReason = "duplicate entry" }),
        };
        Assert.Equal(HttpStatusCode.NoContent, (await manager.SendAsync(withReason)).StatusCode);
        Assert.Equal(1, await AuditCountAsync(_factory, entryId, AuditAction.Deleted));

        await _factory.WithDbContextAsync(async db =>
        {
            var entry = await db.LedgerEntries.FirstAsync(e => e.Id == entryId);
            Assert.Equal(LedgerEntryStatus.Deleted, entry.Status);
            Assert.Equal("duplicate entry", entry.DeletionReason);
        });
    }

    // --- 6.8 ---
    [Fact]
    public async Task Editor_cannot_see_or_edit_another_subsidiarys_entries()
    {
        var subA = await CreateSubsidiaryAsync();
        var subB = await CreateSubsidiaryAsync();
        var (categoryId, _) = await AnyCategoryAsync();
        var manager = await GlobalManagerClientAsync();

        // An entry in B, created by the global manager.
        var bEntry = await manager.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subB, categoryId));
        var bEntryId = (await ReadJsonAsync(bEntry)).GetProperty("id").GetGuid();

        var editorA = await AuthenticatedClientAsync(UserRole.Editor, subA);

        // List as Editor A returns only A's entries (never B's).
        var list = await ReadJsonAsync(await editorA.GetAsync("/api/ledger-entries?pageSize=100"));
        foreach (var item in list.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(subA.ToString(), item.GetProperty("subsidiaryId").GetString());
        }

        // Editing B's entry as Editor A is forbidden.
        var edit = await editorA.PutAsJsonAsync($"/api/ledger-entries/{bEntryId}",
            new { categoryId, amount = 1m, date = "2026-01-01", description = "hack" });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
    }

    // --- 6.9 (completed subsidiary deactivation guard) ---
    [Fact]
    public async Task Subsidiary_with_a_non_terminal_entry_cannot_be_deactivated_until_it_is_gone()
    {
        var manager = await GlobalManagerClientAsync();
        var (categoryId, _) = await AnyCategoryAsync();

        var subCreate = await manager.PostAsJsonAsync("/api/subsidiaries", new
        {
            name = "Ledger Guarded",
            code = $"LG-{Guid.NewGuid():N}"[..10],
            initialBalance = 0m,
            referenceDate = "2026-01-01",
        });
        var subId = (await ReadJsonAsync(subCreate)).GetProperty("id").GetGuid();

        var entry = await manager.PostAsJsonAsync("/api/ledger-entries", EntryPayload(subId, categoryId));
        var entryId = (await ReadJsonAsync(entry)).GetProperty("id").GetGuid();

        // Blocked while the Open (non-terminal) entry exists.
        var blocked = await manager.PatchAsync($"/api/subsidiaries/{subId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("SUBSIDIARY_HAS_NON_TERMINAL_LEDGER_ENTRIES", (await ReadJsonAsync(blocked)).GetProperty("code").GetString());

        // Soft-delete the entry (now terminal) → deactivation succeeds.
        var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/ledger-entries/{entryId}")
        {
            Content = JsonContent.Create(new { deletionReason = "cleanup" }),
        };
        Assert.Equal(HttpStatusCode.NoContent, (await manager.SendAsync(del)).StatusCode);

        var ok = await manager.PatchAsync($"/api/subsidiaries/{subId}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False((await ReadJsonAsync(ok)).GetProperty("isActive").GetBoolean());
    }
}
