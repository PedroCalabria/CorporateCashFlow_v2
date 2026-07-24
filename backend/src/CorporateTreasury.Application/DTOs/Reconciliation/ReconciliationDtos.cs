namespace CorporateTreasury.Application.DTOs.Reconciliation;

/// <summary>Body of <c>POST /api/reconciliation/manual-match</c> — links one ledger entry to one unmatched line (transition 3b).</summary>
public sealed record ManualMatchRequest(Guid LedgerEntryId, Guid BankStatementLineId);

/// <summary>Body of <c>POST /api/reconciliation/{id}/justify</c> — the Editor's mandatory justification (transition 5).</summary>
public sealed record JustifyRequest(string JustificationText);

/// <summary>Body of <c>POST /api/reconciliation/{id}/reject</c> — the Manager's mandatory rejection reason (transition 7).</summary>
public sealed record RejectRequest(string RejectionReason);

/// <summary>A ledger entry available for matching or justification (<c>Open</c> or <c>PendingReconciliation</c>).</summary>
public sealed record PendingLedgerEntryDto(
    Guid Id,
    Guid SubsidiaryId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    string Status,
    string? JustificationText,
    string? RejectionReason);

/// <summary>An unmatched bank statement line available to be matched against a pending entry.</summary>
public sealed record UnmatchedBankStatementLineDto(
    Guid Id,
    Guid SubsidiaryId,
    DateOnly Date,
    decimal Amount,
    string Type,
    string Description,
    string? DocumentNumber);

/// <summary>A ledger entry awaiting a Manager decision (<c>PendingApproval</c>) — the approve/reject queue.</summary>
public sealed record PendingApprovalEntryDto(
    Guid Id,
    Guid SubsidiaryId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    string? JustificationText);

/// <summary>
/// The reconciliation board scoped to the caller: entries available for matching/justification, the
/// unmatched lines to match them against, and (for Managers) the pending-approval queue.
/// </summary>
public sealed record ReconciliationBoardDto(
    IReadOnlyList<PendingLedgerEntryDto> PendingEntries,
    IReadOnlyList<UnmatchedBankStatementLineDto> UnmatchedLines,
    IReadOnlyList<PendingApprovalEntryDto> PendingApprovals);
