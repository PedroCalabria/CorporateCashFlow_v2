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

    /// <summary>
    /// Creates an <c>Active</c> user (the <c>user-management</c> creation path, business-rules §3
    /// transition 1). Enforces the invariant that an <see cref="UserRole.Editor"/> is always tied
    /// to a subsidiary. Who is <em>allowed</em> to create which role/scope is an authorization
    /// concern owned by the Application layer, not here.
    /// </summary>
    public static User Create(string name, string email, string passwordHash, UserRole role, Guid? subsidiaryId)
    {
        GuardEditorHasSubsidiary(role, subsidiaryId);

        var now = DateTime.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            SubsidiaryId = subsidiaryId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Edits role/scope (business-rules §3, transition 4), preserving the Editor invariant.</summary>
    public void UpdateRoleAndScope(UserRole role, Guid? subsidiaryId)
    {
        GuardEditorHasSubsidiary(role, subsidiaryId);
        Role = role;
        SubsidiaryId = subsidiaryId;
        Touch();
    }

    /// <summary>Soft-deactivate (business-rules §3, transition 2). Never a physical delete.</summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    /// <summary>Reactivate a deactivated user (business-rules §3, transition 3).</summary>
    public void Reactivate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>Replace the stored password hash (Manager-forced reset). Never the plaintext.</summary>
    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    // An Editor is always subsidiary-scoped (docs/requirements-document.md §2). This is a
    // last-resort domain guard; the Application validator surfaces the same rule as a 400.
    private static void GuardEditorHasSubsidiary(UserRole role, Guid? subsidiaryId)
    {
        if (role == UserRole.Editor && subsidiaryId is null)
        {
            throw new ArgumentException("An Editor must be tied to a subsidiary.", nameof(subsidiaryId));
        }
    }
}
