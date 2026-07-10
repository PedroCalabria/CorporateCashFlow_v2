namespace CorporateTreasury.Application.DTOs.Auth;

/// <summary>
/// The authenticated user's profile, returned by <c>GET /api/auth/me</c> so the frontend
/// can render the real signed-in user (name/role) in the App Shell and restore it after a
/// silent refresh on boot. <c>SubsidiaryId</c> is null for a global-scoped user.
/// </summary>
public sealed record CurrentUserResponse(Guid Id, string Name, string Email, string Role, Guid? SubsidiaryId);
