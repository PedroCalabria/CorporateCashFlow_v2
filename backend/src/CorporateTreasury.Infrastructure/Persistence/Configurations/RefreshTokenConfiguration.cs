using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="RefreshToken"/>. The token hash is indexed (refresh looks
/// tokens up by hash) and there is an FK to <see cref="User"/> with cascade delete. The
/// computed <c>IsActive</c>/<c>IsExpired</c> flags are ignored (not persisted).
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.HasIndex(t => t.TokenHash);

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.RevokedAt);
        builder.Property(t => t.ReplacedByTokenId);

        builder.Ignore(t => t.IsActive);
        builder.Ignore(t => t.IsExpired);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
