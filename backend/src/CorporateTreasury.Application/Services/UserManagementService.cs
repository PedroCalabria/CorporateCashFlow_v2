using CorporateTreasury.Application.DTOs.Users;
using CorporateTreasury.Application.Exceptions;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Owns user provisioning and lifecycle plus the Global-vs-Subsidiary-Manager scope asymmetry
/// (design.md §D2). The controller applies the coarse Manager gate; this service — reading
/// <see cref="ICurrentUserService"/> — enforces the fine-grained rules and throws
/// <see cref="ForbiddenOperationException"/> (→ 403) on any violation. Returns <c>null</c> for a
/// not-found target so the controller can map it to 404. Request shape (valid role, unique email,
/// Editor ⇒ subsidiary, password strength) is validated before these methods run.
/// </summary>
public sealed class UserManagementService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IInitialPasswordGenerator _initialPasswordGenerator;
    private readonly ICurrentUserService _currentUser;

    public UserManagementService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IInitialPasswordGenerator initialPasswordGenerator,
        ICurrentUserService currentUser)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _initialPasswordGenerator = initialPasswordGenerator;
        _currentUser = currentUser;
    }

    /// <summary>Scope of the acting Manager: <c>null</c> = global (unrestricted); otherwise their subsidiary.</summary>
    private Guid? ActingScope => _currentUser.SubsidiaryId;

    private bool IsGlobalManager => ActingScope is null;

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var role = ParseRole(request.Role);
        EnsureCanAssign(role, request.SubsidiaryId);

        var initialPassword = _initialPasswordGenerator.Generate();
        var user = User.Create(
            request.Name,
            request.Email,
            _passwordHasher.Hash(initialPassword),
            role,
            request.SubsidiaryId);

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new CreateUserResponse(ToResponse(user), initialPassword);
    }

    public async Task<IReadOnlyList<UserResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        // A Subsidiary Manager sees only their subsidiary; a Global Manager (null scope) sees all.
        var users = await _users.ListAsync(ActingScope, cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var role = ParseRole(request.Role);
        EnsureCanActOn(user);
        EnsureCanAssign(role, request.SubsidiaryId);
        EnsureNotSelfDemotion(user, role);

        user.UpdateRoleAndScope(role, request.SubsidiaryId);
        await _users.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<UserResponse?> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        EnsureCanActOn(user);
        if (user.Id == _currentUser.UserId)
        {
            throw new ForbiddenOperationException("You cannot deactivate your own account.");
        }

        user.Deactivate();
        await _users.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<UserResponse?> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        EnsureCanActOn(user);
        user.Reactivate();
        await _users.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    /// <summary>
    /// Sets a new password and revokes the user's outstanding refresh tokens, so a session created
    /// before the reset cannot be renewed (design.md §D4). Returns <c>false</c> if not found.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        EnsureCanActOn(user);

        user.SetPasswordHash(_passwordHasher.Hash(request.NewPassword));
        await _users.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        return true;
    }

    // --- Scope rules (the asymmetry lives here, in one place) ---

    /// <summary>A Subsidiary Manager may act only on users of their own subsidiary.</summary>
    private void EnsureCanActOn(User target)
    {
        if (!IsGlobalManager && target.SubsidiaryId != ActingScope)
        {
            throw new ForbiddenOperationException("You can only manage users within your own subsidiary.");
        }
    }

    /// <summary>
    /// A Subsidiary Manager may only assign Editor/Auditor and only within their own subsidiary —
    /// never Manager, never global scope, never another subsidiary. A Global Manager is unrestricted.
    /// </summary>
    private void EnsureCanAssign(UserRole role, Guid? subsidiaryId)
    {
        if (IsGlobalManager)
        {
            return;
        }

        if (role == UserRole.Manager)
        {
            throw new ForbiddenOperationException("A subsidiary manager cannot assign the Manager role.");
        }

        if (subsidiaryId != ActingScope)
        {
            throw new ForbiddenOperationException("A subsidiary manager can only assign users to their own subsidiary.");
        }
    }

    /// <summary>Guard against a Manager demoting their own account out of the Manager role (lockout safety).</summary>
    private void EnsureNotSelfDemotion(User target, UserRole newRole)
    {
        if (target.Id == _currentUser.UserId && newRole != UserRole.Manager)
        {
            throw new ForbiddenOperationException("You cannot remove the Manager role from your own account.");
        }
    }

    private static UserRole ParseRole(string role) =>
        Enum.Parse<UserRole>(role, ignoreCase: true);

    private static UserResponse ToResponse(User u) =>
        new(u.Id, u.Name, u.Email, u.Role.ToString(), u.SubsidiaryId, u.IsActive);
}
