namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// The three roles that drive RBAC across the system (docs/requirements-document.md §8).
/// A user has exactly one role; scope (global vs. a single subsidiary) is a separate
/// concern carried by <c>User.SubsidiaryId</c>.
/// </summary>
public enum UserRole
{
    Manager,
    Editor,
    Auditor,
}
