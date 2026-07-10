using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence;

/// <summary>
/// EF Core write-model database context.
/// Intentionally empty in the project-bootstrap change: no entities, no
/// <see cref="DbSet{TEntity}"/>s and no migrations exist yet. Entity
/// configurations and sets are added by the capabilities that introduce them.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}
