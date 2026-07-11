using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.UnitTests.Users;

/// <summary>
/// Domain unit tests for the <see cref="User"/> state machine (task 6.1): creation defaults, the
/// Editor ⇒ subsidiary invariant, and the activate/deactivate/edit transitions.
/// </summary>
public sealed class UserTests
{
    private static readonly Guid SubsidiaryA = Guid.NewGuid();

    [Fact]
    public void Create_makes_an_active_user_with_timestamps()
    {
        var user = User.Create("Ada", "ada@example.com", "hash", UserRole.Auditor, subsidiaryId: null);

        Assert.True(user.IsActive);
        Assert.Equal(UserRole.Auditor, user.Role);
        Assert.Null(user.SubsidiaryId);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void Create_requires_a_subsidiary_for_an_editor()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("Bob", "bob@example.com", "hash", UserRole.Editor, subsidiaryId: null));
    }

    [Fact]
    public void Create_allows_an_editor_bound_to_a_subsidiary()
    {
        var user = User.Create("Bob", "bob@example.com", "hash", UserRole.Editor, SubsidiaryA);

        Assert.Equal(UserRole.Editor, user.Role);
        Assert.Equal(SubsidiaryA, user.SubsidiaryId);
    }

    [Fact]
    public void Deactivate_then_reactivate_flips_is_active()
    {
        var user = User.Create("Ada", "ada@example.com", "hash", UserRole.Manager, subsidiaryId: null);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void UpdateRoleAndScope_updates_and_preserves_the_editor_invariant()
    {
        var user = User.Create("Ada", "ada@example.com", "hash", UserRole.Auditor, subsidiaryId: null);

        user.UpdateRoleAndScope(UserRole.Editor, SubsidiaryA);
        Assert.Equal(UserRole.Editor, user.Role);
        Assert.Equal(SubsidiaryA, user.SubsidiaryId);

        Assert.Throws<ArgumentException>(() => user.UpdateRoleAndScope(UserRole.Editor, subsidiaryId: null));
    }
}
