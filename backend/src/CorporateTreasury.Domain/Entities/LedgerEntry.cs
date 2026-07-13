using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// An internal ledger entry — the central aggregate of the system
/// (docs/business-rules-formalization.md §1). This capability implements only transitions
/// <b>1</b> (create → <see cref="LedgerEntryStatus.Open"/>), <b>2</b> (edit while <c>Open</c>), and
/// <b>9</b> (soft-delete). The reconciliation transitions (3/3b/4/5/6/7/8) are owned by the
/// <c>bank-statement-import</c> and <c>reconciliation</c> capabilities and are left as loud stubs.
/// </summary>
public class LedgerEntry
{
    private LedgerEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid SubsidiaryId { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>Credit/Debit, derived from the category's income/expense type at creation (design.md §D3).</summary>
    public LedgerEntryType Type { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly Date { get; private set; }

    public string Description { get; private set; } = null!;

    public LedgerEntryStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Mandatory reason captured on soft-delete (§1.2 rule 9).</summary>
    public string? DeletionReason { get; private set; }

    /// <summary>Set when disputing a reconciliation divergence — populated by the reconciliation capability.</summary>
    public string? JustificationText { get; private set; }

    /// <summary>
    /// Creates an <c>Open</c> entry (transition 1). <paramref name="type"/> is derived by the caller
    /// from the category's <see cref="CategoryType"/> (Income → Credit, Expense → Debit).
    /// </summary>
    public static LedgerEntry Create(
        Guid subsidiaryId,
        Guid categoryId,
        LedgerEntryType type,
        decimal amount,
        DateOnly date,
        string description,
        Guid createdBy)
    {
        var now = DateTime.UtcNow;
        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            SubsidiaryId = subsidiaryId,
            CategoryId = categoryId,
            Type = type,
            Amount = amount,
            Date = date,
            Description = description,
            Status = LedgerEntryStatus.Open,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedBy = createdBy,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Edits the mutable fields (transition 2). Allowed only while <see cref="LedgerEntryStatus.Open"/>.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>Open</c>.</exception>
    public void UpdateDetails(Guid categoryId, LedgerEntryType type, decimal amount, DateOnly date, string description, Guid updatedBy)
    {
        if (Status != LedgerEntryStatus.Open)
        {
            throw new InvalidStateTransitionException(
                "Only an Open ledger entry can be edited.",
                "LEDGER_ENTRY_NOT_OPEN");
        }

        CategoryId = categoryId;
        Type = type;
        Amount = amount;
        Date = date;
        Description = description;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes the entry (transition 9). Allowed from any state; a non-empty
    /// <paramref name="reason"/> is mandatory (§1.2 rule 9, resolved decision §6.3).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The reason is missing, or the entry is already deleted.</exception>
    public void SoftDelete(string reason, Guid deletedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidStateTransitionException(
                "A deletion reason is required.",
                "LEDGER_ENTRY_DELETION_REASON_REQUIRED");
        }

        if (Status == LedgerEntryStatus.Deleted)
        {
            throw new InvalidStateTransitionException(
                "The ledger entry is already deleted.",
                "LEDGER_ENTRY_ALREADY_DELETED");
        }

        Status = LedgerEntryStatus.Deleted;
        DeletionReason = reason;
        DeletedBy = deletedBy;
        DeletedAt = DateTime.UtcNow;
        UpdatedBy = deletedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>True while the entry occupies a non-terminal state (blocks subsidiary deactivation, §4).</summary>
    public bool IsNonTerminal =>
        Status is LedgerEntryStatus.Open or LedgerEntryStatus.PendingReconciliation or LedgerEntryStatus.PendingApproval;

    /// <summary>
    /// Duplicate-detection signature (§1.4): the canonical composite of
    /// <c>SubsidiaryId + CategoryId + Amount + Date + Description</c> that the spreadsheet import uses
    /// to reject a row duplicating an entry already persisted for the subsidiary. Mirrors the
    /// duplicate-prevention rule of <c>BankStatementImportBatch</c> (§2) and is the pattern that
    /// capability will reuse.
    /// </summary>
    public LedgerEntrySignature Signature =>
        LedgerEntrySignature.Of(SubsidiaryId, CategoryId, Amount, Date, Description);

    // --- Reconciliation transitions (3, 3b, 4, 5, 6, 7, 8) — NOT implemented in this capability. ---
    // These are owned by bank-statement-import and reconciliation. They are declared here so the
    // remaining state machine is visible in one place and so any premature call fails loudly rather
    // than silently corrupting state. Their real signatures are designed by the owning capabilities.

    /// <summary>Transition 3: system auto-match → Reconciled. TODO(bank-statement-import).</summary>
    public void MarkReconciledByAutoMatch() =>
        throw new NotImplementedException("TODO(bank-statement-import): §1.2 transition 3 (auto-match → Reconciled).");

    /// <summary>Transition 3b: Editor manual match → Reconciled. TODO(reconciliation).</summary>
    public void MarkReconciledByManualMatch() =>
        throw new NotImplementedException("TODO(reconciliation): §1.2 transition 3b (manual match → Reconciled).");

    /// <summary>Transition 4: no auto-match → PendingReconciliation. TODO(bank-statement-import).</summary>
    public void FlagPendingReconciliation() =>
        throw new NotImplementedException("TODO(bank-statement-import): §1.2 transition 4 (no match → PendingReconciliation).");

    /// <summary>Transition 5: Editor submits justification → PendingApproval. TODO(reconciliation).</summary>
    public void SubmitJustification() =>
        throw new NotImplementedException("TODO(reconciliation): §1.2 transition 5 (justification → PendingApproval).");

    /// <summary>Transition 6: Manager approves → Reconciled. TODO(reconciliation).</summary>
    public void Approve() =>
        throw new NotImplementedException("TODO(reconciliation): §1.2 transition 6 (approve → Reconciled).");

    /// <summary>Transition 7: Manager rejects → PendingReconciliation. TODO(reconciliation).</summary>
    public void Reject() =>
        throw new NotImplementedException("TODO(reconciliation): §1.2 transition 7 (reject → PendingReconciliation).");

    /// <summary>Transition 8: batch rejection reverts → PendingReconciliation. TODO(bank-statement-import).</summary>
    public void RevertOnBatchRejection() =>
        throw new NotImplementedException("TODO(bank-statement-import): §1.2 transition 8 (batch rejected → PendingReconciliation).");
}
