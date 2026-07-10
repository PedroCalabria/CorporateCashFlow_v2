using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IRefreshTokenRepository"/>.</summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _db.RefreshTokens.AddAsync(token, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
