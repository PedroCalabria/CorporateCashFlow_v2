using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="BankStatementLine"/> (docs/requirements-document.md §3.7). The FK to
/// the batch is configured on the batch side (cascade). The FK to Subsidiaries is <c>Restrict</c>. The
/// nullable <see cref="BankStatementLine.MatchedLedgerEntryId"/> is a <c>Restrict</c> FK to
/// LedgerEntries reserved for the reconciliation capability. Enums are stored as strings; money as
/// <c>decimal(18,2)</c>. Indexes on ImportBatchId/SubsidiaryId/Status support scoped queries.
/// </summary>
public sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ToTable("BankStatementLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(l => l.Date).IsRequired();
        builder.Property(l => l.Description).IsRequired().HasMaxLength(500);
        builder.Property(l => l.DocumentNumber).HasMaxLength(100);

        builder.HasOne<Subsidiary>()
            .WithMany()
            .HasForeignKey(l => l.SubsidiaryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reserved for reconciliation — a nullable link to the matched ledger entry (never set here).
        builder.HasOne<LedgerEntry>()
            .WithMany()
            .HasForeignKey(l => l.MatchedLedgerEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.ImportBatchId);
        builder.HasIndex(l => l.SubsidiaryId);
        builder.HasIndex(l => l.Status);
    }
}
