using CorporateTreasury.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CorporateTreasury.Infrastructure;

/// <summary>
/// Composition helpers for the Infrastructure layer. Keeps provider-specific
/// dependencies (Npgsql, Hangfire.PostgreSql) inside Infrastructure while the
/// Api project remains the composition root that calls into this.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core write model and Hangfire (PostgreSQL-backed) storage.
    /// No entities and no jobs are registered yet — this is wiring only.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' was not configured.");

        // EF Core write model — empty context, no migrations run on startup.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Hangfire uses the same PostgreSQL instance for its job storage.
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString)));

        // Background processing server — no jobs are defined in this change.
        services.AddHangfireServer();

        return services;
    }
}
