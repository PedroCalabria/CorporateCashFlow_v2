namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// The lifecycle states of a <c>LedgerEntry</c> (docs/business-rules-formalization.md §1.1).
/// The full set is defined here, but the <c>ledger-entries</c> capability only drives
/// <see cref="Open"/> and <see cref="Deleted"/>; the reconciliation states are reached by the
/// <c>bank-statement-import</c> and <c>reconciliation</c> capabilities.
/// </summary>
public enum LedgerEntryStatus
{
    /// <summary>Created, not yet matched against any bank statement. Editable by the Editor.</summary>
    Open,

    /// <summary>No automatic match found, or a previous match was invalidated. (Not yet implemented.)</summary>
    PendingReconciliation,

    /// <summary>Editor submitted a justification/correction, awaiting Manager approval. (Not yet implemented.)</summary>
    PendingApproval,

    /// <summary>Manager approved or the system auto-matched. Terminal for balance purposes. (Not yet implemented.)</summary>
    Reconciled,

    /// <summary>Soft-deleted (terminal). The row is preserved for audit.</summary>
    Deleted,
}
