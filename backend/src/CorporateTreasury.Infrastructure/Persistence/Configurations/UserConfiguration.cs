using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="User"/>. Email is unique (login lookup key). The role is
/// stored as its string name for readable, migration-stable values. <c>SubsidiaryId</c> is now
/// a real (nullable) foreign key to <c>Subsidiaries</c>, wired by the subsidiaries capability;
/// there is no navigation property by design (the User aggregate stays lean).
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.SubsidiaryId);

        // Real FK to Subsidiaries (added with the subsidiaries capability). No navigation
        // property — the FK is enough. Restrict so a subsidiary with users cannot be hard-deleted
        // (subsidiaries are soft-deactivated anyway, guarded by the active-user check).
        builder.HasOne<Subsidiary>()
            .WithMany()
            .HasForeignKey(u => u.SubsidiaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired();
    }
}
