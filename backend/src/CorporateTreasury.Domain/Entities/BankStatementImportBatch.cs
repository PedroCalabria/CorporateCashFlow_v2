using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// One uploaded bank-statement spreadsheet and its lines — the aggregate root of the
/// <c>bank-statement-import</c> capability (docs/requirements-document.md §3.6;
/// docs/business-rules-formalization.md §2). This capability implements transition <b>1</b>
/// (create → <see cref="BankStatementBatchStatus.Processed"/>/<see cref="BankStatementBatchStatus.ProcessedWithErrors"/>)
/// and transition <b>2</b> (<see cref="Reject"/> → <see cref="BankStatementBatchStatus.Rejected"/>,
/// every line <see cref="BankStatementLineStatus.Invalidated"/>). The reversal of any matched
/// <c>LedgerEntry</c> to <c>PendingReconciliation</c> (§1.2 rule 8) is owned by <c>reconciliation</c>.
/// </summary>
public class BankStatementImportBatch
{
    private readonly List<BankStatementLine> _lines = new();

    private BankStatementImportBatch()
    {
    }

    public Guid Id { get; private set; }

    public Guid SubsidiaryId { get; private set; }

    public Guid ImportedBy { get; private set; }

    public DateTime ImportedAt { get; private set; }

    public string FileName { get; private set; } = null!;

    public BankStatementBatchStatus Status { get; private set; }

    public Guid? RejectedBy { get; private set; }

    public DateTime? RejectedAt { get; private set; }

    /// <summary>Mandatory reason captured on rejection (§2.2 transition 2).</summary>
    public string? RejectionReason { get; private set; }

    public IReadOnlyCollection<BankStatementLine> Lines => _lines;

    /// <summary>
    /// Creates a batch from the already-validated lines (transition 1). The status is
    /// <see cref="BankStatementBatchStatus.ProcessedWithErrors"/> when the import rejected at least one
    /// row (<paramref name="hadRejectedRows"/>), otherwise <see cref="BankStatementBatchStatus.Processed"/>.
    /// </summary>
    public static BankStatementImportBatch Create(
        Guid subsidiaryId,
        Guid importedBy,
        string fileName,
        bool hadRejectedRows)
    {
        return new BankStatementImportBatch
        {
            Id = Guid.NewGuid(),
            SubsidiaryId = subsidiaryId,
            ImportedBy = importedBy,
            ImportedAt = DateTime.UtcNow,
            FileName = fileName,
            Status = hadRejectedRows ? BankStatementBatchStatus.ProcessedWithErrors : BankStatementBatchStatus.Processed,
        };
    }

    /// <summary>
    /// Adds a line built for this batch. Used by the import service after a row passes validation and
    /// the duplicate check. The line's <c>ImportBatchId</c> must already reference this batch.
    /// </summary>
    public void AddLine(BankStatementLine line) => _lines.Add(line);

    /// <summary>
    /// Rejects the entire batch (transition 2): sets <see cref="BankStatementBatchStatus.Rejected"/>
    /// with <see cref="RejectedBy"/>/<see cref="RejectedAt"/>, invalidates every line, and records the
    /// mandatory <paramref name="reason"/>. All-or-nothing — no selective per-line invalidation in the
    /// MVP (§2.3).
    /// </summary>
    /// <exception cref="InvalidStateTransitionException">The reason is missing, or the batch is not in a rejectable state.</exception>
    public void Reject(string reason, Guid rejectedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidStateTransitionException(
                "A rejection reason is required.",
                "BATCH_REJECTION_REASON_REQUIRED");
        }

        if (Status is not (BankStatementBatchStatus.Processed or BankStatementBatchStatus.ProcessedWithErrors))
        {
            throw new InvalidStateTransitionException(
                "Only a processed batch can be rejected.",
                "BATCH_NOT_REJECTABLE");
        }

        Status = BankStatementBatchStatus.Rejected;
        RejectionReason = reason;
        RejectedBy = rejectedBy;
        RejectedAt = DateTime.UtcNow;

        foreach (var line in _lines)
        {
            line.Invalidate();
        }

        // TODO(reconciliation): revert any LedgerEntry matched (auto or manual) from this batch back to
        // PendingReconciliation (docs/business-rules-formalization.md §1.2 rule 8). Those match links only
        // exist once the reconciliation capability is implemented, so there is nothing to revert here yet.
    }
}
