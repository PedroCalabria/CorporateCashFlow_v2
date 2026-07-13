using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.UnitTests.LedgerEntries;

/// <summary>
/// Domain unit tests for the <see cref="LedgerEntry"/> transitions this capability implements —
/// 1 (create → Open, type derived), 2 (edit only while Open), 9 (soft-delete needs a reason,
/// works from any state). The central state machine, so regressions here are the most damaging.
/// </summary>
public sealed class LedgerEntryTests
{
    private static readonly Guid Subsidiary = Guid.NewGuid();
    private static readonly Guid Category = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static LedgerEntry NewOpenEntry(LedgerEntryType type = LedgerEntryType.Credit) =>
        LedgerEntry.Create(Subsidiary, Category, type, 100m, new DateOnly(2026, 1, 10), "Test", Actor);

    [Fact]
    public void Create_starts_open_with_the_derived_type()
    {
        var credit = LedgerEntry.Create(Subsidiary, Category, LedgerEntryType.Credit, 100m, new DateOnly(2026, 1, 10), "Sale", Actor);
        var debit = LedgerEntry.Create(Subsidiary, Category, LedgerEntryType.Debit, 50m, new DateOnly(2026, 1, 11), "Rent", Actor);

        Assert.Equal(LedgerEntryStatus.Open, credit.Status);
        Assert.Equal(LedgerEntryType.Credit, credit.Type);
        Assert.Equal(LedgerEntryType.Debit, debit.Type);
        Assert.Equal(Actor, credit.CreatedBy);
        Assert.True(credit.IsNonTerminal);
    }

    [Fact]
    public void UpdateDetails_is_allowed_while_open()
    {
        var entry = NewOpenEntry();

        entry.UpdateDetails(Category, LedgerEntryType.Debit, 250m, new DateOnly(2026, 2, 1), "Updated", Actor);

        Assert.Equal(250m, entry.Amount);
        Assert.Equal("Updated", entry.Description);
        Assert.Equal(LedgerEntryType.Debit, entry.Type);
    }

    [Fact]
    public void UpdateDetails_throws_when_not_open()
    {
        var entry = NewOpenEntry();
        entry.SoftDelete("mistake", Actor); // now Deleted (non-Open)

        var ex = Assert.Throws<InvalidStateTransitionException>(
            () => entry.UpdateDetails(Category, LedgerEntryType.Credit, 1m, new DateOnly(2026, 3, 1), "x", Actor));
        Assert.Equal("LEDGER_ENTRY_NOT_OPEN", ex.Code);
    }

    [Fact]
    public void SoftDelete_requires_a_reason()
    {
        var entry = NewOpenEntry();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => entry.SoftDelete("   ", Actor));
        Assert.Equal("LEDGER_ENTRY_DELETION_REASON_REQUIRED", ex.Code);
        Assert.NotEqual(LedgerEntryStatus.Deleted, entry.Status);
    }

    [Fact]
    public void SoftDelete_with_a_reason_marks_deleted_and_records_it()
    {
        var entry = NewOpenEntry();

        entry.SoftDelete("duplicate", Actor);

        Assert.Equal(LedgerEntryStatus.Deleted, entry.Status);
        Assert.Equal("duplicate", entry.DeletionReason);
        Assert.Equal(Actor, entry.DeletedBy);
        Assert.NotNull(entry.DeletedAt);
        Assert.False(entry.IsNonTerminal);
    }
}
