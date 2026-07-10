using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence;

/// <summary>
/// EF Core write-model database context. The <c>auth</c> capability introduces the first
/// real domain tables (<see cref="Users"/>, <see cref="RefreshTokens"/>) and the first
/// migration. Entity configurations are applied from this assembly.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
