using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the fixed <see cref="Category"/> catalog. <c>Code</c> is unique; type stored as its string name.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
    }
}
