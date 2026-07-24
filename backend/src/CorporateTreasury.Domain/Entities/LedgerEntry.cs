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

    /// <summary>Set when disputing a reconciliation divergence — the Editor's mandatory justification (§1.2 transition 5).</summary>
    public string? JustificationText { get; private set; }

    /// <summary>The Manager's mandatory reason when rejecting a submitted justification (§1.2 transition 7).</summary>
    public string? RejectionReason { get; private set; }

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

    // --- Reconciliation transitions (3, 3b, 4, 5, 6, 7, 8) — owned by the reconciliation capability. ---
    // The authoritative who/when record for each of these is the AuditLog row the Application service
    // writes alongside the call (design.md §D4); the entity carries only the current divergence state.

    /// <summary>
    /// Transition 3: the automatic match pass linked this entry to a bank statement line → Reconciled.
    /// Allowed only from <c>Open</c> (an unambiguous auto-match candidate is always <c>Open</c>).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>Open</c>.</exception>
    public void MarkReconciledByAutoMatch()
    {
        if (Status != LedgerEntryStatus.Open)
        {
            throw new InvalidStateTransitionException(
                "Only an Open ledger entry can be auto-matched.",
                "LEDGER_ENTRY_NOT_OPEN_FOR_MATCH");
        }

        Status = LedgerEntryStatus.Reconciled;
    }

    /// <summary>
    /// Transition 3b: an Editor manually linked this entry to an unmatched line → Reconciled directly,
    /// with no Manager approval (§6 decision #5). Allowed from <c>Open</c> or <c>PendingReconciliation</c>.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>Open</c> or <c>PendingReconciliation</c>.</exception>
    public void MarkReconciledByManualMatch()
    {
        if (Status is not (LedgerEntryStatus.Open or LedgerEntryStatus.PendingReconciliation))
        {
            throw new InvalidStateTransitionException(
                "Only an Open or PendingReconciliation ledger entry can be manually matched.",
                "LEDGER_ENTRY_NOT_MATCHABLE");
        }

        Status = LedgerEntryStatus.Reconciled;
    }

    /// <summary>
    /// Transition 4: the automatic match pass found no candidate for this entry → PendingReconciliation.
    /// Allowed only from <c>Open</c>.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>Open</c>.</exception>
    public void FlagPendingReconciliation()
    {
        if (Status != LedgerEntryStatus.Open)
        {
            throw new InvalidStateTransitionException(
                "Only an Open ledger entry can be flagged for reconciliation.",
                "LEDGER_ENTRY_NOT_OPEN_FOR_PENDING");
        }

        Status = LedgerEntryStatus.PendingReconciliation;
    }

    /// <summary>
    /// Transition 5: the Editor submits a mandatory, non-empty justification → PendingApproval. The entry
    /// is thereby locked from further Editor edits (it is no longer <c>Open</c>).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>PendingReconciliation</c>, or the justification is empty.</exception>
    public void SubmitJustification(string justificationText)
    {
        if (Status != LedgerEntryStatus.PendingReconciliation)
        {
            throw new InvalidStateTransitionException(
                "Only a PendingReconciliation ledger entry can be justified.",
                "LEDGER_ENTRY_NOT_PENDING_RECONCILIATION");
        }

        if (string.IsNullOrWhiteSpace(justificationText))
        {
            throw new InvalidStateTransitionException(
                "A justification is required.",
                "LEDGER_ENTRY_JUSTIFICATION_REQUIRED");
        }

        JustificationText = justificationText;
        RejectionReason = null;
        Status = LedgerEntryStatus.PendingApproval;
    }

    /// <summary>Transition 6: the Manager approves the justification → Reconciled. No reason is required (§6 decision #2).</summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>PendingApproval</c>.</exception>
    public void Approve()
    {
        if (Status != LedgerEntryStatus.PendingApproval)
        {
            throw new InvalidStateTransitionException(
                "Only a PendingApproval ledger entry can be approved.",
                "LEDGER_ENTRY_NOT_PENDING_APPROVAL");
        }

        Status = LedgerEntryStatus.Reconciled;
    }

    /// <summary>
    /// Transition 7: the Manager rejects the justification → back to PendingReconciliation, with a
    /// mandatory, non-empty reason (§6 decision #2). The entry re-enters the justification cycle.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>PendingApproval</c>, or the reason is empty.</exception>
    public void Reject(string rejectionReason)
    {
        if (Status != LedgerEntryStatus.PendingApproval)
        {
            throw new InvalidStateTransitionException(
                "Only a PendingApproval ledger entry can be rejected.",
                "LEDGER_ENTRY_NOT_PENDING_APPROVAL");
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new InvalidStateTransitionException(
                "A rejection reason is required.",
                "LEDGER_ENTRY_REJECTION_REASON_REQUIRED");
        }

        RejectionReason = rejectionReason;
        Status = LedgerEntryStatus.PendingReconciliation;
    }

    /// <summary>
    /// Transition 8: a <c>Reconciled</c> entry matched through a rejected batch reverts to
    /// PendingReconciliation. The batch's single <paramref name="batchReason"/> covers all reverted
    /// entries — <b>no</b> new per-entry justification is required (§6 decision #4). The entry simply
    /// awaits a corrected import and a fresh match attempt.
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The entry is not <c>Reconciled</c>.</exception>
    public void RevertOnBatchRejection(Guid batchId, string batchReason)
    {
        if (Status != LedgerEntryStatus.Reconciled)
        {
            throw new InvalidStateTransitionException(
                "Only a Reconciled ledger entry can be reverted by a batch rejection.",
                "LEDGER_ENTRY_NOT_RECONCILED");
        }

        Status = LedgerEntryStatus.PendingReconciliation;
    }
}
