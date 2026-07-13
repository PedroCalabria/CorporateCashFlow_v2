namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// The lifecycle states of a <c>BankStatementLine</c> (docs/business-rules-formalization.md §2.3).
/// The <c>bank-statement-import</c> capability drives only <see cref="Unmatched"/> (on creation) and
/// <see cref="Invalidated"/> (on batch rejection). <see cref="AutoMatched"/> and
/// <see cref="ManuallyMatched"/> are reached by the <c>reconciliation</c> capability and are declared
/// here so the state machine is visible in one place.
/// </summary>
public enum BankStatementLineStatus
{
    /// <summary>Imported, not yet matched against any ledger entry.</summary>
    Unmatched,

    /// <summary>Auto-matched to a ledger entry by the reconciliation engine. (Not yet implemented.)</summary>
    AutoMatched,

    /// <summary>Manually matched to a ledger entry by an Editor. (Not yet implemented.)</summary>
    ManuallyMatched,

    /// <summary>Invalidated because the batch it belongs to was rejected (terminal). The row is preserved for audit.</summary>
    Invalidated,
}
