namespace CorporateTreasury.Application.DTOs.Users;

/// <summary>
/// Body of <c>POST /api/users</c>. No password field — the backend generates the initial password
/// and returns it once (design.md §D3). <c>Role</c> is a string (`Manager`/`Editor`/`Auditor`),
/// mirroring the response convention from the auth capability; the validator rejects invalid values.
/// </summary>
public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Role,
    Guid? SubsidiaryId);

/// <summary>Body of <c>PUT /api/users/{id}</c> — role/scope only.</summary>
public sealed record UpdateUserRequest(
    string Role,
    Guid? SubsidiaryId);

/// <summary>Body of <c>PATCH /api/users/{id}/reset-password</c> — the Manager-chosen new password.</summary>
public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>Read model for a user. Never carries the password hash.</summary>
public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? SubsidiaryId,
    bool IsActive);

/// <summary>Create response — the user plus the one-time initial password (shown once, never retrievable again).</summary>
public sealed record CreateUserResponse(
    UserResponse User,
    string InitialPassword);
