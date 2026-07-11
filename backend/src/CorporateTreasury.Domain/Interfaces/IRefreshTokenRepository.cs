using CorporateTreasury.Domain.Entities;

namespace CorporateTreasury.Domain.Interfaces;

/// <summary>
/// Persistence for <see cref="RefreshToken"/>s, the server-side state that makes refresh
/// tokens revocable and single-use (rotation). Implemented by Infrastructure (EF Core).
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke every currently-active (non-revoked, non-expired) refresh token of a user and persist.
    /// Used on a Manager-forced password reset so an old session cannot outlive the change.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persist pending changes (e.g. revocation flags set during rotation/logout).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
