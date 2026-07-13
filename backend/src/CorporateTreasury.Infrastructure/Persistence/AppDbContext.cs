using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence;

/// <summary>
/// EF Core write-model database context. The <c>auth</c> capability introduced the first
/// real domain tables (<see cref="Users"/>, <see cref="RefreshTokens"/>); the <c>subsidiaries</c>
/// capability adds <see cref="Subsidiaries"/> and <see cref="BankAccounts"/>. Entity
/// configurations are applied from this assembly.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Subsidiary> Subsidiaries => Set<Subsidiary>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
