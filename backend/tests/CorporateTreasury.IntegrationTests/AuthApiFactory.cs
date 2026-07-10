using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using CorporateTreasury.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CorporateTreasury.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (Testcontainers). Runs in the
/// Development environment so <c>Program</c>'s startup applies EF migrations and the dev seed,
/// giving the tests a real schema. Exposes helpers to seed users and inspect/mutate refresh
/// tokens directly in the database.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        // WebApplicationBuilder captures configuration eagerly, so an environment variable
        // (picked up by the default config with higher priority than appsettings.json) is
        // the reliable way to point EF Core AND Hangfire at the throwaway container.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _database.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Insert a user with a hashed password, returning the persisted entity.</summary>
    public async Task<User> SeedUserAsync(
        string email,
        string password,
        bool isActive = true,
        UserRole role = UserRole.Editor,
        Guid? subsidiaryId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = email,
            PasswordHash = hasher.Hash(password),
            Role = role,
            SubsidiaryId = subsidiaryId,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Run an action against a fresh <see cref="AppDbContext"/> scope.</summary>
    public async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    public async Task<RefreshToken?> GetLatestRefreshTokenAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.RefreshTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
