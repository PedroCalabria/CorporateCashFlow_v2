using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// One line of an uploaded bank statement — the external/bank source of truth
/// (docs/requirements-document.md §3.7). Created <see cref="BankStatementLineStatus.Unmatched"/> and
/// (in this capability) only ever moves to <see cref="BankStatementLineStatus.Invalidated"/> when its
/// batch is rejected. The <see cref="AutoMatch"/>/<see cref="ManuallyMatch"/> transitions and
/// <see cref="MatchedLedgerEntryId"/> are owned by the <c>reconciliation</c> capability and left as
/// loud stubs / reserved columns.
/// </summary>
public class BankStatementLine
{
    private BankStatementLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid ImportBatchId { get; private set; }

    public Guid SubsidiaryId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Credit/Debit as stated by the bank file (§3.7).</summary>
    public LedgerEntryType Type { get; private set; }

    public string Description { get; private set; } = null!;

    /// <summary>Optional bank reference (§3.7).</summary>
    public string? DocumentNumber { get; private set; }

    /// <summary>Nullable FK reserved for the reconciliation capability; never set in this capability.</summary>
    public Guid? MatchedLedgerEntryId { get; private set; }

    public BankStatementLineStatus Status { get; private set; }

    /// <summary>Creates an <c>Unmatched</c> line for the given batch/subsidiary.</summary>
    public static BankStatementLine Create(
        Guid importBatchId,
        Guid subsidiaryId,
        DateOnly date,
        decimal amount,
        LedgerEntryType type,
        string description,
        string? documentNumber)
    {
        return new BankStatementLine
        {
            Id = Guid.NewGuid(),
            ImportBatchId = importBatchId,
            SubsidiaryId = subsidiaryId,
            Date = date,
            Amount = amount,
            Type = type,
            Description = description,
            DocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber.Trim(),
            Status = BankStatementLineStatus.Unmatched,
        };
    }

    /// <summary>Invalidates the line when its batch is rejected (§2.2 transition 2). Called by <see cref="BankStatementImportBatch.Reject"/>.</summary>
    public void Invalidate() => Status = BankStatementLineStatus.Invalidated;

    /// <summary>
    /// Duplicate-detection signature (§1.4/§3.7): the composite of
    /// <c>SubsidiaryId + Date + Amount + Description</c> that the statement import uses to reject a row
    /// duplicating a line already persisted for the subsidiary.
    /// </summary>
    public BankStatementLineSignature Signature =>
        BankStatementLineSignature.Of(SubsidiaryId, Date, Amount, Description);

    // --- Reconciliation transitions (§2.3) — owned by the reconciliation capability. ---

    /// <summary>Links this line to a ledger entry by the automatic match pass → AutoMatched. Only an <c>Unmatched</c> line can be matched.</summary>
    /// <exception cref="InvalidStateTransitionException">The line is not <c>Unmatched</c>.</exception>
    public void AutoMatch(Guid ledgerEntryId)
    {
        EnsureUnmatched();
        MatchedLedgerEntryId = ledgerEntryId;
        Status = BankStatementLineStatus.AutoMatched;
    }

    /// <summary>Links this line to a ledger entry chosen by an Editor → ManuallyMatched. Only an <c>Unmatched</c> line can be matched.</summary>
    /// <exception cref="InvalidStateTransitionException">The line is not <c>Unmatched</c>.</exception>
    public void ManuallyMatch(Guid ledgerEntryId)
    {
        EnsureUnmatched();
        MatchedLedgerEntryId = ledgerEntryId;
        Status = BankStatementLineStatus.ManuallyMatched;
    }

    /// <summary>Breaks the match link (used by the batch-rejection cascade before/with <see cref="Invalidate"/>).</summary>
    public void ClearMatch() => MatchedLedgerEntryId = null;

    private void EnsureUnmatched()
    {
        if (Status != BankStatementLineStatus.Unmatched)
        {
            throw new InvalidStateTransitionException(
                "Only an unmatched bank statement line can be matched.",
                "BANK_STATEMENT_LINE_NOT_UNMATCHED");
        }
    }
}
