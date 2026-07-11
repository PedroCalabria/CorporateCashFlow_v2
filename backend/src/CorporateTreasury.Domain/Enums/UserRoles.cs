namespace CorporateTreasury.Domain.Enums;

/// <summary>
/// Helpers for working with role names that arrive as strings on the API boundary (request DTOs
/// carry <c>Role</c> as text, mirroring the auth capability's response convention). Centralizes
/// parse/validate so validators and services agree on what a valid role is.
/// </summary>
public static class UserRoles
{
    /// <summary>True if <paramref name="role"/> names a valid <see cref="UserRole"/> (case-insensitive).</summary>
    public static bool IsValid(string? role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: true, out _);

    /// <summary>True if <paramref name="role"/> parses to exactly <paramref name="target"/>.</summary>
    public static bool Is(string? role, UserRole target) =>
        Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed) && parsed == target;
}
