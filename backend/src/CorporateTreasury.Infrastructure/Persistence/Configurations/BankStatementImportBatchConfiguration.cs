using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="BankStatementImportBatch"/> (docs/requirements-document.md §3.6).
/// The FK to Subsidiaries is <c>Restrict</c> (batches are never cascaded away). Its lines are a
/// one-to-many owned-by-navigation relationship configured on the line side. Enums are stored as
/// strings; indexes on SubsidiaryId/Status support the scoped list filters.
/// </summary>
public sealed class BankStatementImportBatchConfiguration : IEntityTypeConfiguration<BankStatementImportBatch>
{
    public void Configure(EntityTypeBuilder<BankStatementImportBatch> builder)
    {
        builder.ToTable("BankStatementImportBatches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).IsRequired().HasMaxLength(260);
        builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.ImportedAt).IsRequired();
        builder.Property(b => b.RejectionReason).HasMaxLength(1000);

        builder.HasOne<Subsidiary>()
            .WithMany()
            .HasForeignKey(b => b.SubsidiaryId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many to its lines, mapped through the private backing field exposed as Lines.
        builder.HasMany(b => b.Lines)
            .WithOne()
            .HasForeignKey(l => l.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(BankStatementImportBatch.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(b => b.SubsidiaryId);
        builder.HasIndex(b => b.Status);
    }
}
