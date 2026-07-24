using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.UnitTests.BankStatementImports;

/// <summary>
/// Domain unit tests for the <see cref="BankStatementImportBatch"/> transitions this capability
/// implements — 1 (create → Processed/ProcessedWithErrors, lines Unmatched) and 2 (Reject → Rejected,
/// every line Invalidated, reason mandatory). A real state-machine transition, so regressions here are
/// the most damaging (docs/development-workflow.md §4).
/// </summary>
public sealed class BankStatementImportBatchTests
{
    private static readonly Guid Subsidiary = Guid.NewGuid();
    private static readonly Guid Importer = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static BankStatementImportBatch NewBatch(bool hadRejectedRows = false, int lines = 2)
    {
        var batch = BankStatementImportBatch.Create(Subsidiary, Importer, "statement.csv", hadRejectedRows);
        for (var i = 0; i < lines; i++)
        {
            batch.AddLine(BankStatementLine.Create(
                batch.Id, Subsidiary, new DateOnly(2026, 1, 10 + i), 100m + i, LedgerEntryType.Credit, $"Line {i}", null));
        }

        return batch;
    }

    [Fact]
    public void Create_resolves_processed_when_no_row_was_rejected()
    {
        var batch = NewBatch(hadRejectedRows: false);

        Assert.Equal(BankStatementBatchStatus.Processed, batch.Status);
        Assert.Equal(Importer, batch.ImportedBy);
        Assert.All(batch.Lines, l => Assert.Equal(BankStatementLineStatus.Unmatched, l.Status));
    }

    [Fact]
    public void Create_resolves_processed_with_errors_when_a_row_was_rejected()
    {
        var batch = NewBatch(hadRejectedRows: true);

        Assert.Equal(BankStatementBatchStatus.ProcessedWithErrors, batch.Status);
    }

    [Fact]
    public void New_lines_are_unmatched()
    {
        var line = BankStatementLine.Create(Guid.NewGuid(), Subsidiary, new DateOnly(2026, 1, 5), 10m, LedgerEntryType.Debit, "x", null);

        Assert.Equal(BankStatementLineStatus.Unmatched, line.Status);
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        var batch = NewBatch();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => batch.Reject("   ", Manager));
        Assert.Equal("BATCH_REJECTION_REASON_REQUIRED", ex.Code);
        Assert.NotEqual(BankStatementBatchStatus.Rejected, batch.Status);
        Assert.All(batch.Lines, l => Assert.Equal(BankStatementLineStatus.Unmatched, l.Status));
    }

    [Fact]
    public void Reject_with_a_reason_rejects_the_batch_and_invalidates_every_line()
    {
        var batch = NewBatch(lines: 3);

        batch.Reject("wrong period", Manager);

        Assert.Equal(BankStatementBatchStatus.Rejected, batch.Status);
        Assert.Equal(Manager, batch.RejectedBy);
        Assert.NotNull(batch.RejectedAt);
        Assert.Equal("wrong period", batch.RejectionReason);
        Assert.All(batch.Lines, l => Assert.Equal(BankStatementLineStatus.Invalidated, l.Status));
    }

    [Fact]
    public void Reject_is_not_allowed_from_a_terminal_state()
    {
        var batch = NewBatch();
        batch.Reject("first", Manager);

        var ex = Assert.Throws<InvalidStateTransitionException>(() => batch.Reject("again", Manager));
        Assert.Equal("BATCH_NOT_REJECTABLE", ex.Code);
    }
}
