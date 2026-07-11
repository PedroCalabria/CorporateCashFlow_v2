using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Subsidiary"/>. <c>Code</c> is unique. The 1:1 relationship to
/// <see cref="BankAccount"/> is configured here (the subsidiary is the principal); the account is
/// required, so a subsidiary always has exactly one (design.md §D3/§D6).
/// </summary>
public sealed class SubsidiaryConfiguration : IEntityTypeConfiguration<Subsidiary>
{
    public void Configure(EntityTypeBuilder<Subsidiary> builder)
    {
        builder.ToTable("Subsidiaries");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(s => s.Code).IsUnique();

        builder.Property(s => s.IsActive)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // 1:1 — the bank account depends on the subsidiary and is loaded with it.
        builder.HasOne(s => s.BankAccount)
            .WithOne()
            .HasForeignKey<BankAccount>(b => b.SubsidiaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.BankAccount).IsRequired();
    }
}
