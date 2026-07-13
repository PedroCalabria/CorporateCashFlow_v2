using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ICategoryRepository"/> over the fixed catalog.</summary>
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Category>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Categories.OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Categories.AnyAsync(c => c.Id == id, cancellationToken);
}
