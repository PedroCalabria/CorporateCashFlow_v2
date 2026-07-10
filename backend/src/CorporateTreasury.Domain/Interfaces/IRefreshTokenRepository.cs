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

    /// <summary>Persist pending changes (e.g. revocation flags set during rotation/logout).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
