using CorporateTreasury.Application.DTOs.Auth;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Orchestrates login, refresh (with rotation), and logout over the password hasher,
/// token service, and repositories. Called directly by the Api controller (no MediatR
/// in this project — design.md §D1).
/// </summary>
/// <remarks>
/// Both "unknown email" and "wrong password" — and an <c>Inactive</c> account — return
/// the same generic failure (a <c>null</c> result), so responses never reveal which
/// input was wrong (no user enumeration; spec "Login with invalid credentials"). Every
/// attempt still writes an <c>AccessLog</c> row (design.md §D2): this is the only layer that
/// already resolves the looked-up <c>User?</c> before that collapse, so it's the only place
/// with the nullable-<c>UserId</c> distinction the audit-trail capability needs — the internal
/// write never leaks back into the HTTP response.
/// </remarks>
public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAccessLogRepository _accessLogs;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IAccessLogRepository accessLogs,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _accessLogs = accessLogs;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Verify credentials and, on success for an active user, issue an access token and a
    /// freshly persisted refresh token. Returns <c>null</c> for any failure (unknown email,
    /// wrong password, or inactive account) — the caller maps that to a generic 401. Every
    /// branch writes an <c>AccessLog</c> row (<see cref="AccessLogEventType.LoginSuccess"/> or
    /// <see cref="AccessLogEventType.LoginFailed"/>), with <c>UserId</c> null only when the
    /// email matched no user at all.
    /// </summary>
    public async Task<AuthTokens?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        // Same generic failure whether the email is unknown, the password wrong, or the
        // account inactive — no user enumeration. The AccessLog row still records the
        // richer, internal-only UserId distinction; it never reaches the HTTP response.
        if (user is null || !user.IsActive)
        {
            await _accessLogs.AddAsync(AccessLog.Create(user?.Id, AccessLogEventType.LoginFailed, ipAddress), cancellationToken);
            await _accessLogs.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            await _accessLogs.AddAsync(AccessLog.Create(user.Id, AccessLogEventType.LoginFailed, ipAddress), cancellationToken);
            await _accessLogs.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Staged here, flushed by the SaveChangesAsync inside IssueTokensAsync (same DbContext,
        // same unit of work as the refresh token it persists).
        await _accessLogs.AddAsync(AccessLog.Create(user.Id, AccessLogEventType.LoginSuccess, ipAddress), cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Validate the presented refresh token against the store and, if active, rotate it:
    /// revoke the old token, issue a new access + refresh pair. Returns <c>null</c> if the
    /// token is missing, unknown, expired, or already revoked.
    /// </summary>
    public async Task<AuthTokens?> RefreshAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return null;
        }

        var hash = _tokenService.HashRefreshToken(rawRefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(hash, cancellationToken);

        if (existing is null || !existing.IsActive)
        {
            return null;
        }

        var user = await _users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var tokens = await IssueTokensAsync(user, cancellationToken);

        // Rotation: single-use the presented token and link it to its replacement.
        existing.RevokedAt = DateTime.UtcNow;
        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    /// <summary>
    /// Revoke the presented refresh token so it can never renew again. Idempotent and
    /// silent: an unknown or already-revoked token is a no-op (logout always "succeeds").
    /// </summary>
    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return;
        }

        var hash = _tokenService.HashRefreshToken(rawRefreshToken);
        var existing = await _refreshTokens.GetByTokenHashAsync(hash, cancellationToken);

        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _refreshTokens.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refresh = _tokenService.GenerateRefreshToken();

        await _refreshTokens.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refresh.TokenHash,
                ExpiresAt = refresh.ExpiresAt,
                CreatedAt = DateTime.UtcNow,
            },
            cancellationToken);
        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken, refresh.RawToken, refresh.ExpiresAt);
    }
}
