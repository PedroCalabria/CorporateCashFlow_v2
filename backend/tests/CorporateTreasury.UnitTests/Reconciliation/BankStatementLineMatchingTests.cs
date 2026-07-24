using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.UnitTests.Reconciliation;

/// <summary>
/// Domain unit tests for the <see cref="BankStatementLine"/> matching transitions (§2.3): a line moves
/// <c>Unmatched → AutoMatched | ManuallyMatched</c>, only from <c>Unmatched</c>, and its match link can
/// be cleared by the batch-rejection cascade.
/// </summary>
public sealed class BankStatementLineMatchingTests
{
    private static readonly Guid Subsidiary = Guid.NewGuid();

    private static BankStatementLine NewLine() =>
        BankStatementLine.Create(Guid.NewGuid(), Subsidiary, new DateOnly(2026, 1, 5), 100m, LedgerEntryType.Credit, "Deposit", null);

    [Fact]
    public void AutoMatch_sets_status_and_link()
    {
        var line = NewLine();
        var entryId = Guid.NewGuid();

        line.AutoMatch(entryId);

        Assert.Equal(BankStatementLineStatus.AutoMatched, line.Status);
        Assert.Equal(entryId, line.MatchedLedgerEntryId);
    }

    [Fact]
    public void ManuallyMatch_sets_status_and_link()
    {
        var line = NewLine();
        var entryId = Guid.NewGuid();

        line.ManuallyMatch(entryId);

        Assert.Equal(BankStatementLineStatus.ManuallyMatched, line.Status);
        Assert.Equal(entryId, line.MatchedLedgerEntryId);
    }

    [Fact]
    public void Matching_an_already_matched_line_throws()
    {
        var line = NewLine();
        line.AutoMatch(Guid.NewGuid());

        var ex = Assert.Throws<InvalidStateTransitionException>(() => line.ManuallyMatch(Guid.NewGuid()));
        Assert.Equal("BANK_STATEMENT_LINE_NOT_UNMATCHED", ex.Code);
    }

    [Fact]
    public void ClearMatch_breaks_the_link()
    {
        var line = NewLine();
        line.AutoMatch(Guid.NewGuid());

        line.ClearMatch();

        Assert.Null(line.MatchedLedgerEntryId);
    }
}
