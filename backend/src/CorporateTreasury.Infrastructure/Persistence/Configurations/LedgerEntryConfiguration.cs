using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="LedgerEntry"/>. FKs to Subsidiaries and Categories are
/// <c>Restrict</c> (entries are soft-deleted, never cascaded away). Enums are stored as strings;
/// money as <c>decimal(18,2)</c>. Indexes on SubsidiaryId/Status/Date support the scoped list filters.
/// </summary>
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.DeletionReason).HasMaxLength(500);
        builder.Property(e => e.JustificationText).HasMaxLength(1000);

        builder.HasOne<Subsidiary>()
            .WithMany()
            .HasForeignKey(e => e.SubsidiaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SubsidiaryId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Date);
    }
}
