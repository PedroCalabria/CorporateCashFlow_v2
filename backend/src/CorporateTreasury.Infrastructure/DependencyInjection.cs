using CorporateTreasury.Application.Common;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Application.Services;
using CorporateTreasury.Domain.Interfaces;
using CorporateTreasury.Infrastructure.Auth;
using CorporateTreasury.Infrastructure.Persistence;
using CorporateTreasury.Infrastructure.Persistence.Repositories;
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

        // --- Auth (auth capability) ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<AuthService>();

        // --- Subsidiaries (subsidiaries capability) ---
        services.AddScoped<ISubsidiaryRepository, SubsidiaryRepository>();
        services.AddScoped<SubsidiaryService>();

        // --- User management (user-management capability) ---
        services.AddSingleton<IInitialPasswordGenerator, InitialPasswordGenerator>();
        services.AddScoped<UserManagementService>();

        // --- Ledger entries (ledger-entries capability) ---
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<LedgerEntryService>();

        // --- Bank statement import (bank-statement-import capability) ---
        services.AddScoped<IBankStatementImportRepository, BankStatementImportRepository>();
        services.AddScoped<BankStatementImportService>();

        // --- Reconciliation (reconciliation capability) ---
        var reconciliationSettings = configuration.GetSection(ReconciliationSettings.SectionName).Get<ReconciliationSettings>()
            ?? new ReconciliationSettings();
        services.AddSingleton(reconciliationSettings);
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
