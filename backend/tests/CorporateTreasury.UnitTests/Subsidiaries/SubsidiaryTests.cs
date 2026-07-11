using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Exceptions;

namespace CorporateTreasury.UnitTests.Subsidiaries;

/// <summary>
/// Domain unit tests for the <see cref="Subsidiary"/> state transitions (task 6.1). These guard
/// the most damaging, least-visible regressions — the deactivation integrity rule and the
/// activate/deactivate transitions themselves (docs/development-workflow.md §4).
/// </summary>
public sealed class SubsidiaryTests
{
    private static Subsidiary NewSubsidiary() =>
        Subsidiary.Create("Acme North", "ACME-N", 1000m, new DateOnly(2026, 1, 1));

    [Fact]
    public void Create_builds_an_active_subsidiary_with_its_bank_account()
    {
        var subsidiary = Subsidiary.Create("Acme North", "ACME-N", 2500.50m, new DateOnly(2026, 3, 15));

        Assert.True(subsidiary.IsActive);
        Assert.NotNull(subsidiary.BankAccount);
        Assert.Equal(subsidiary.Id, subsidiary.BankAccount.SubsidiaryId);
        Assert.Equal(2500.50m, subsidiary.BankAccount.InitialBalance);
        Assert.Equal(new DateOnly(2026, 3, 15), subsidiary.BankAccount.ReferenceDate);
    }

    [Fact]
    public void Deactivate_throws_when_active_users_are_still_assigned()
    {
        var subsidiary = NewSubsidiary();

        var ex = Assert.Throws<InvalidStateTransitionException>(() => subsidiary.Deactivate(activeUserCount: 1));

        Assert.Equal("SUBSIDIARY_HAS_ACTIVE_USERS", ex.Code);
        Assert.True(subsidiary.IsActive); // unchanged — the guard blocked the transition
    }

    [Fact]
    public void Deactivate_succeeds_when_no_active_users_are_assigned()
    {
        var subsidiary = NewSubsidiary();

        subsidiary.Deactivate(activeUserCount: 0);

        Assert.False(subsidiary.IsActive);
    }

    [Fact]
    public void Reactivate_returns_the_subsidiary_to_active()
    {
        var subsidiary = NewSubsidiary();
        subsidiary.Deactivate(activeUserCount: 0);

        subsidiary.Reactivate();

        Assert.True(subsidiary.IsActive);
    }
}
