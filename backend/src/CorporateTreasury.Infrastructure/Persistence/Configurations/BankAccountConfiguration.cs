using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="BankAccount"/> (1:1 with <see cref="Subsidiary"/>). The
/// <c>SubsidiaryId</c> foreign key is configured from the <see cref="SubsidiaryConfiguration"/>
/// side. <c>InitialBalance</c> uses money-appropriate precision; <c>ReferenceDate</c> maps to a
/// plain <c>date</c>. Both are the immutable baseline (design.md §D4).
/// </summary>
public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.InitialBalance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(b => b.ReferenceDate)
            .IsRequired();

        // One account per subsidiary — enforce the 1:1 at the database level too.
        builder.HasIndex(b => b.SubsidiaryId).IsUnique();
    }
}
