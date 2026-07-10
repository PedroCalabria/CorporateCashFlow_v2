using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// An authenticated principal. This is the identity foundation every future
/// capability reads through <c>ICurrentUserService</c> to apply RBAC.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SubsidiaryId"/> is nullable: <c>null</c> means global scope, a value
/// means the user is tied to exactly one subsidiary. The <c>Subsidiary</c> entity does
/// not exist yet (it arrives with the <c>subsidiaries</c> capability), so this is a plain
/// nullable value with no FK/navigation for now — see design.md §D1.
/// </para>
/// <para>
/// Creating/editing/deactivating users belongs to <c>user-management</c>; this capability
/// only reads users (login) and seeds one temporary dev Manager.
/// </para>
/// </remarks>
public class User
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Email { get; set; }

    /// <summary>PBKDF2 hash produced by <c>IPasswordHasher</c>. Never the plaintext password.</summary>
    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    /// <summary><c>null</c> = global scope; otherwise the single subsidiary this user is tied to.</summary>
    public Guid? SubsidiaryId { get; set; }

    /// <summary>An <c>Inactive</c> user (false) is blocked from logging in (business-rules §3).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
