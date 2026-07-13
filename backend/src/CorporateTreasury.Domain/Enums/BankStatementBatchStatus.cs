namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// The lifecycle states of a <c>BankStatementImportBatch</c> (docs/business-rules-formalization.md
/// §2.1). The <c>bank-statement-import</c> capability drives all three: a batch is created
/// <see cref="Processed"/> or <see cref="ProcessedWithErrors"/> depending on whether any row was
/// rejected at import time (§2.2 transition 1), and a Manager may move it to <see cref="Rejected"/>
/// (transition 2).
/// </summary>
public enum BankStatementBatchStatus
{
    /// <summary>All rows imported successfully — no duplicates or invalid rows.</summary>
    Processed,

    /// <summary>Some rows were rejected at import time (duplicate/invalid); the rest were persisted.</summary>
    ProcessedWithErrors,

    /// <summary>A Manager rejected the entire batch (terminal); every line is <c>Invalidated</c>.</summary>
    Rejected,
}
