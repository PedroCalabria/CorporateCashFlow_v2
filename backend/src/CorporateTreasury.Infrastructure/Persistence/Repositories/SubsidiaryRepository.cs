using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISubsidiaryRepository"/>.</summary>
public sealed class SubsidiaryRepository : ISubsidiaryRepository
{
    private readonly AppDbContext _db;

    public SubsidiaryRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Subsidiary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Subsidiaries
            .Include(s => s.BankAccount)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Subsidiary>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Subsidiaries
            .Include(s => s.BankAccount)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        _db.Subsidiaries.AnyAsync(
            s => s.Code == code && (excludeId == null || s.Id != excludeId),
            cancellationToken);

    public Task<int> CountActiveUsersAsync(Guid subsidiaryId, CancellationToken cancellationToken = default) =>
        _db.Users.CountAsync(u => u.SubsidiaryId == subsidiaryId && u.IsActive, cancellationToken);

    public async Task AddAsync(Subsidiary subsidiary, CancellationToken cancellationToken = default) =>
        await _db.Subsidiaries.AddAsync(subsidiary, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
