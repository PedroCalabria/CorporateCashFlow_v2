using System.Globalization;
using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.BankStatementImports;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Owns the bank-statement-import lifecycle (§2.2 transitions 1 and 2) plus its role/scope
/// authorization and the automatic audit write on rejection (design.md §D2/§D5). The controller
/// applies only <c>[Authorize]</c>; this service enforces the matrix reading
/// <see cref="ICurrentUserService"/> and throws <see cref="ForbiddenOperationException"/> (→ 403) on
/// any violation. Not-found targets return <c>null</c>/<c>false</c> for the controller to map to 404.
/// </summary>
public sealed class BankStatementImportService
{
    private const string EntityType = nameof(BankStatementImportBatch);

    private readonly IBankStatementImportRepository _batches;
    private readonly ILedgerEntryRepository _entries;
    private readonly IAuditLogRepository _audit;
    private readonly ISubsidiaryRepository _subsidiaries;
    private readonly ICurrentUserService _currentUser;
    private readonly IReconciliationService _reconciliation;

    public BankStatementImportService(
        IBankStatementImportRepository batches,
        ILedgerEntryRepository entries,
        IAuditLogRepository audit,
        ISubsidiaryRepository subsidiaries,
        ICurrentUserService currentUser,
        IReconciliationService reconciliation)
    {
        _batches = batches;
        _entries = entries;
        _audit = audit;
        _subsidiaries = subsidiaries;
        _currentUser = currentUser;
        _reconciliation = reconciliation;
    }

    private Guid ActingUserId => _currentUser.UserId ?? throw new ForbiddenOperationException("No authenticated user.");

    /// <summary>
    /// Import a bank statement for a single (scoped) subsidiary. The file format is chosen by
    /// <paramref name="fileName"/>'s extension (<c>.xlsx</c> → Excel, otherwise CSV); both parsers emit
    /// the same structural rows. Valid rows are persisted as <c>Unmatched</c> lines; invalid rows —
    /// including rows that duplicate a line already persisted for the subsidiary (§1.4/§3.7) — are
    /// reported (never silently dropped). The batch resolves to <c>Processed</c> or
    /// <c>ProcessedWithErrors</c> (§2.2 transition 1).
    /// </summary>
    public async Task<ImportResult> ImportAsync(Guid subsidiaryId, Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        EnsureWriter();
        EnsureCanActOnSubsidiary(subsidiaryId);
        await EnsureSubsidiaryActiveAsync(subsidiaryId, cancellationToken);

        var parsed = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? BankStatementXlsxParser.Parse(fileStream)
            : BankStatementCsvParser.Parse(await ReadAllTextAsync(fileStream, cancellationToken));
        var errors = new List<ImportRowError>(parsed.Errors.Select(e => new ImportRowError(e.RowNumber, e.Message)));

        // §1.4/§3.7 duplicate detection: seed with the signatures already persisted for this subsidiary,
        // then grow the set as we accept rows — so a row is rejected whether it duplicates an existing
        // line (e.g. re-importing the same file) or an earlier row in the same file.
        var seenSignatures = new HashSet<BankStatementLineSignature>(
            await _batches.GetExistingLineSignaturesAsync(subsidiaryId, cancellationToken));

        // The batch id is needed on each line, so build the batch first (status is fixed up after the loop).
        var acceptedRows = new List<(DateOnly Date, decimal Amount, LedgerEntryType Type, string Description, string? DocumentNumber)>();
        foreach (var row in parsed.Rows)
        {
            if (!DateOnly.TryParse(row.Date, CultureInfo.InvariantCulture, out var date))
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Invalid date '{row.Date}' (expected yyyy-MM-dd)."));
                continue;
            }

            if (!decimal.TryParse(row.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Invalid amount '{row.Amount}' (must be a positive number)."));
                continue;
            }

            if (!TryParseType(row.Type, out var type))
            {
                errors.Add(new ImportRowError(row.RowNumber, $"Invalid type '{row.Type}' (expected Credit or Debit)."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Description))
            {
                errors.Add(new ImportRowError(row.RowNumber, "Description is required."));
                continue;
            }

            var description = row.Description.Trim();
            if (!seenSignatures.Add(BankStatementLineSignature.Of(subsidiaryId, date, amount, description)))
            {
                errors.Add(new ImportRowError(row.RowNumber, "Duplicate of an existing bank statement line"));
                continue;
            }

            acceptedRows.Add((date, amount, type, description, row.DocumentNumber));
        }

        var batch = BankStatementImportBatch.Create(subsidiaryId, ActingUserId, fileName, hadRejectedRows: errors.Count > 0);
        foreach (var r in acceptedRows)
        {
            batch.AddLine(BankStatementLine.Create(batch.Id, subsidiaryId, r.Date, r.Amount, r.Type, r.Description, r.DocumentNumber));
        }

        await _batches.AddAsync(batch, cancellationToken);

        // Automatic matching runs synchronously in the same unit of work (design.md §D2): matched entries
        // and lines, plus the leftover PendingReconciliation flags, commit atomically with the batch below.
        await _reconciliation.RunAutoMatchAsync(batch, cancellationToken);

        await _batches.SaveChangesAsync(cancellationToken);

        return new ImportResult(batch.Id, batch.Status.ToString(), acceptedRows.Count, errors.OrderBy(e => e.RowNumber).ToList());
    }

    public async Task<PagedResult<BankStatementBatchResponse>> ListAsync(BankStatementBatchFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        // Reads are open to every role, scoped to the caller: an Editor or subsidiary-scoped
        // Manager/Auditor sees only their subsidiary (non-null scope); a global reader sees all.
        var query = new BankStatementBatchQuery(
            ScopeSubsidiaryId: _currentUser.SubsidiaryId,
            SubsidiaryId: filter.SubsidiaryId,
            Status: ParseStatus(filter.Status),
            DateFrom: filter.DateFrom,
            DateTo: filter.DateTo,
            Skip: paging.Skip,
            Take: paging.Take);

        var (items, total) = await _batches.ListAsync(query, cancellationToken);
        return new PagedResult<BankStatementBatchResponse>(
            items.Select(ToResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    /// <summary>Manager-only full-batch rejection with a mandatory reason (§2.2 transition 2). Returns <c>false</c> if not found.</summary>
    public async Task<bool> RejectAsync(Guid id, RejectBatchRequest request, CancellationToken cancellationToken = default)
    {
        // Coarse role gate before loading — only a Manager may reject; others get 403 even for a missing id.
        EnsureManager();

        var batch = await _batches.GetByIdAsync(id, cancellationToken);
        if (batch is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(batch.SubsidiaryId);

        var oldSnapshot = AuditSnapshot.Of(batch);
        batch.Reject(request.RejectionReason, ActingUserId);
        await _audit.AddAsync(
            AuditLog.Create(EntityType, batch.Id, AuditAction.Rejected, ActingUserId, oldSnapshot, AuditSnapshot.Of(batch)),
            cancellationToken);

        // §1.2 rule 8 / §2.2 transition 2 cascade (design.md §D5): every LedgerEntry matched (auto or
        // manual) through this batch and still Reconciled reverts to PendingReconciliation. The single
        // batch-level RejectionReason covers all of them — no new per-entry justification (§6 decision #4).
        // Committed in the same SaveChanges below so the whole rejection-plus-cascade is atomic.
        await RevertMatchedEntriesAsync(batch, request.RejectionReason, cancellationToken);

        await _batches.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Reverts every <c>Reconciled</c> ledger entry matched through <paramref name="batch"/>'s lines back to
    /// <c>PendingReconciliation</c> (transition 8) and breaks the match links, writing one <c>Reverted</c>
    /// audit row per affected entry referencing the batch and its reason. Does not save — the caller's
    /// single <c>SaveChangesAsync</c> commits it atomically with the batch rejection.
    /// </summary>
    private async Task RevertMatchedEntriesAsync(BankStatementImportBatch batch, string reason, CancellationToken cancellationToken)
    {
        var matchedLines = batch.Lines.Where(l => l.MatchedLedgerEntryId is not null).ToList();
        if (matchedLines.Count == 0)
        {
            return;
        }

        var entryIds = matchedLines.Select(l => l.MatchedLedgerEntryId!.Value).Distinct().ToList();
        var entries = (await _entries.GetByIdsAsync(entryIds, cancellationToken))
            .ToDictionary(e => e.Id);

        foreach (var line in matchedLines)
        {
            if (entries.TryGetValue(line.MatchedLedgerEntryId!.Value, out var entry)
                && entry.Status == LedgerEntryStatus.Reconciled)
            {
                var before = AuditSnapshot.Of(entry);
                entry.RevertOnBatchRejection(batch.Id, reason);
                await _audit.AddAsync(
                    AuditLog.Create(
                        nameof(LedgerEntry),
                        entry.Id,
                        AuditAction.Reverted,
                        ActingUserId,
                        before,
                        RevertSnapshot(entry, batch.Id, reason)),
                    cancellationToken);
            }

            line.ClearMatch();
        }
    }

    private static string RevertSnapshot(LedgerEntry entry, Guid batchId, string reason) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            entry.Id,
            entry.SubsidiaryId,
            Status = entry.Status.ToString(),
            Reason = "BatchRejected",
            BatchId = batchId,
            RejectionReason = reason,
        });

    // --- Authorization helpers (the role/scope matrix, centralized) ---

    private void EnsureWriter()
    {
        if (_currentUser.Role == UserRole.Auditor)
        {
            throw new ForbiddenOperationException("Auditors have read-only access.");
        }
    }

    private void EnsureManager()
    {
        if (_currentUser.Role != UserRole.Manager)
        {
            throw new ForbiddenOperationException("Only a Manager can reject a bank statement batch.");
        }
    }

    /// <summary>A subsidiary-scoped user (Editor or scoped Manager) may act only within their own subsidiary; a global Manager may act anywhere.</summary>
    private void EnsureCanActOnSubsidiary(Guid subsidiaryId)
    {
        if (_currentUser.SubsidiaryId is Guid scope && subsidiaryId != scope)
        {
            throw new ForbiddenOperationException("You can only act on bank statements within your own subsidiary.");
        }
    }

    private async Task EnsureSubsidiaryActiveAsync(Guid subsidiaryId, CancellationToken cancellationToken)
    {
        var subsidiary = await _subsidiaries.GetByIdAsync(subsidiaryId, cancellationToken);
        if (subsidiary is null || !subsidiary.IsActive)
        {
            throw new InvalidStateTransitionException(
                "Bank statements cannot be imported for an inactive or unknown subsidiary.",
                "SUBSIDIARY_INACTIVE");
        }
    }

    private static async Task<string> ReadAllTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static bool TryParseType(string value, out LedgerEntryType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type);

    private static BankStatementBatchStatus? ParseStatus(string? status) =>
        Enum.TryParse<BankStatementBatchStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;

    private static BankStatementBatchResponse ToResponse(BankStatementImportBatch b) => new(
        b.Id,
        b.SubsidiaryId,
        b.FileName,
        b.ImportedBy,
        b.ImportedAt,
        b.Status.ToString(),
        b.Lines.Count,
        b.Lines.Count(l => l.Status == BankStatementLineStatus.Unmatched),
        b.Lines.Count(l => l.Status == BankStatementLineStatus.Invalidated),
        b.RejectedBy,
        b.RejectedAt,
        b.RejectionReason);
}
