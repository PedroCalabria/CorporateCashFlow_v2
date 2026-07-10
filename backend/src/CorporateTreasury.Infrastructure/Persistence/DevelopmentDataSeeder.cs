using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence;

/// <summary>
/// TEMPORARY development-only seed. Creates one fixed global Manager so the application is
/// usable before the <c>user-management</c> capability can create real users. Idempotent
/// (only inserts when the fixed email is absent) and guarded by the caller to run in the
/// Development environment only (design.md §D6). Remove once <c>user-management</c> ships.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>Predictable local dev credentials — never used outside Development.</summary>
    public const string SeedEmail = "manager@corporatetreasury.local";
    public const string SeedPassword = "Password123!";

    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        var alreadySeeded = await db.Users.AnyAsync(u => u.Email == SeedEmail, cancellationToken);
        if (alreadySeeded)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Dev Manager",
            Email = SeedEmail,
            PasswordHash = passwordHasher.Hash(SeedPassword),
            Role = UserRole.Manager,
            SubsidiaryId = null, // global scope
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
