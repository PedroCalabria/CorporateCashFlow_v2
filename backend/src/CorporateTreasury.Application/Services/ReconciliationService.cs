using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.DTOs.Reconciliation;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Common;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Implements the reconciliation engine and divergence workflow (§1.2 transitions 3, 3b, 4, 5, 6, 7).
/// The controller applies only <c>[Authorize]</c>; this service enforces the role/scope matrix reading
/// <see cref="ICurrentUserService"/> and throws <see cref="ForbiddenOperationException"/> (→ 403). Every
/// transition is recorded in the <c>AuditLog</c> (design.md §D4). Writes go through EF Core; the auto-
/// match pass shares the import's unit of work so a match commits atomically with its batch (design.md §D2).
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private const string EntityType = nameof(LedgerEntry);

    private readonly ILedgerEntryRepository _entries;
    private readonly IBankStatementImportRepository _batches;
    private readonly IAuditLogRepository _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ReconciliationSettings _settings;

    public ReconciliationService(
        ILedgerEntryRepository entries,
        IBankStatementImportRepository batches,
        IAuditLogRepository audit,
        ICurrentUserService currentUser,
        ReconciliationSettings settings)
    {
        _entries = entries;
        _batches = batches;
        _audit = audit;
        _currentUser = currentUser;
        _settings = settings;
    }

    private Guid ActingUserId => _currentUser.UserId ?? throw new ForbiddenOperationException("No authenticated user.");

    /// <inheritdoc />
    public async Task RunAutoMatchAsync(BankStatementImportBatch batch, CancellationToken cancellationToken = default)
    {
        // Candidate pool: all Open entries of the subsidiary. Matching consumes from this pool so a line
        // never matches an entry another line already claimed, and the leftovers become PendingReconciliation.
        var openPool = (await _entries.GetOpenEntriesAsync(batch.SubsidiaryId, cancellationToken)).ToList();
        var tolerance = _settings.DateToleranceDays;

        foreach (var line in batch.Lines.Where(l => l.Status == BankStatementLineStatus.Unmatched))
        {
            var candidates = openPool
                .Where(e => e.Amount == line.Amount
                    && Math.Abs(e.Date.DayNumber - line.Date.DayNumber) <= tolerance)
                .ToList();

            // Only an unambiguous single candidate auto-matches; zero or many leave the line unmatched.
            if (candidates.Count != 1)
            {
                continue;
            }

            var entry = candidates[0];
            var before = AuditSnapshot.Of(entry);
            entry.MarkReconciledByAutoMatch();
            line.AutoMatch(entry.Id);
            openPool.Remove(entry);

            await _audit.AddAsync(
                AuditLog.Create(EntityType, entry.Id, AuditAction.Updated, SystemActor.Id, before, AutoMatchSnapshot(entry, line)),
                cancellationToken);
        }

        // Transition 4: every still-Open entry the pass could not match becomes PendingReconciliation.
        foreach (var entry in openPool)
        {
            var before = AuditSnapshot.Of(entry);
            entry.FlagPendingReconciliation();
            await _audit.AddAsync(
                AuditLog.Create(EntityType, entry.Id, AuditAction.Updated, SystemActor.Id, before, AuditSnapshot.Of(entry)),
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<ReconciliationBoardDto> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        // Reads are open to every role (Auditor included), scoped to the caller: a subsidiary-scoped user
        // sees only their subsidiary (non-null scope); a global user sees all.
        var scope = _currentUser.SubsidiaryId;

        var pending = await _entries.GetByStatusesAsync(
            scope,
            new[] { LedgerEntryStatus.Open, LedgerEntryStatus.PendingReconciliation },
            cancellationToken);

        var pendingApprovals = await _entries.GetByStatusesAsync(
            scope,
            new[] { LedgerEntryStatus.PendingApproval },
            cancellationToken);

        var unmatched = await _batches.GetUnmatchedLinesAsync(scope, cancellationToken);

        return new ReconciliationBoardDto(
            pending.Select(ToPendingDto).ToList(),
            unmatched.Select(ToLineDto).ToList(),
            pendingApprovals.Select(ToApprovalDto).ToList());
    }

    /// <inheritdoc />
    public async Task<bool> ManualMatchAsync(ManualMatchRequest request, CancellationToken cancellationToken = default)
    {
        EnsureWriter();

        var entry = await _entries.GetByIdAsync(request.LedgerEntryId, cancellationToken);
        var line = await _batches.GetLineByIdAsync(request.BankStatementLineId, cancellationToken);
        if (entry is null || line is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        if (line.SubsidiaryId != entry.SubsidiaryId)
        {
            throw new InvalidStateTransitionException(
                "The bank statement line belongs to a different subsidiary than the ledger entry.",
                "RECONCILIATION_SUBSIDIARY_MISMATCH");
        }

        if (line.Status != BankStatementLineStatus.Unmatched)
        {
            throw new InvalidStateTransitionException(
                "The bank statement line is already matched or invalidated.",
                "BANK_STATEMENT_LINE_NOT_UNMATCHED");
        }

        var before = AuditSnapshot.Of(entry);
        entry.MarkReconciledByManualMatch();
        line.ManuallyMatch(entry.Id);

        await _audit.AddAsync(
            AuditLog.Create(EntityType, entry.Id, AuditAction.Updated, ActingUserId, before, ManualMatchSnapshot(entry, line)),
            cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> JustifyAsync(Guid ledgerEntryId, JustifyRequest request, CancellationToken cancellationToken = default)
    {
        EnsureWriter();

        var entry = await _entries.GetByIdAsync(ledgerEntryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        var before = AuditSnapshot.Of(entry);
        entry.SubmitJustification(request.JustificationText);

        await _audit.AddAsync(
            AuditLog.Create(EntityType, entry.Id, AuditAction.JustificationSubmitted, ActingUserId, before, AuditSnapshot.Of(entry)),
            cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ApproveAsync(Guid ledgerEntryId, CancellationToken cancellationToken = default)
    {
        EnsureManager();

        var entry = await _entries.GetByIdAsync(ledgerEntryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        var before = AuditSnapshot.Of(entry);
        entry.Approve();

        await _audit.AddAsync(
            AuditLog.Create(EntityType, entry.Id, AuditAction.Approved, ActingUserId, before, AuditSnapshot.Of(entry)),
            cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectAsync(Guid ledgerEntryId, RejectRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();

        var entry = await _entries.GetByIdAsync(ledgerEntryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        EnsureCanActOnSubsidiary(entry.SubsidiaryId);

        var before = AuditSnapshot.Of(entry);
        entry.Reject(request.RejectionReason);

        await _audit.AddAsync(
            AuditLog.Create(EntityType, entry.Id, AuditAction.Rejected, ActingUserId, before, AuditSnapshot.Of(entry)),
            cancellationToken);
        await _entries.SaveChangesAsync(cancellationToken);

        return true;
    }

    // --- Authorization helpers (mirror the other write services, design.md §D6) ---

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
            throw new ForbiddenOperationException("Only a Manager can approve or reject a reconciliation.");
        }
    }

    /// <summary>A subsidiary-scoped user acts only within their own subsidiary; a global Manager acts anywhere.</summary>
    private void EnsureCanActOnSubsidiary(Guid subsidiaryId)
    {
        if (_currentUser.SubsidiaryId is Guid scope && subsidiaryId != scope)
        {
            throw new ForbiddenOperationException("You can only reconcile within your own subsidiary.");
        }
    }

    private static string AutoMatchSnapshot(LedgerEntry entry, BankStatementLine line) =>
        WithMatch(entry, line, "Auto");

    private static string ManualMatchSnapshot(LedgerEntry entry, BankStatementLine line) =>
        WithMatch(entry, line, "Manual");

    private static string WithMatch(LedgerEntry entry, BankStatementLine line, string matchType) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            entry.Id,
            entry.SubsidiaryId,
            Status = entry.Status.ToString(),
            MatchType = matchType,
            MatchedBankStatementLineId = line.Id,
        });

    private static PendingLedgerEntryDto ToPendingDto(LedgerEntry e) => new(
        e.Id, e.SubsidiaryId, e.Type.ToString(), e.Amount, e.Date, e.Description,
        e.Status.ToString(), e.JustificationText, e.RejectionReason);

    private static PendingApprovalEntryDto ToApprovalDto(LedgerEntry e) => new(
        e.Id, e.SubsidiaryId, e.Type.ToString(), e.Amount, e.Date, e.Description, e.JustificationText);

    private static UnmatchedBankStatementLineDto ToLineDto(BankStatementLine l) => new(
        l.Id, l.SubsidiaryId, l.Date, l.Amount, l.Type.ToString(), l.Description, l.DocumentNumber);
}
