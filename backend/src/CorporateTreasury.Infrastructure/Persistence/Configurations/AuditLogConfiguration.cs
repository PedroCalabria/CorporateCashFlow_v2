using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AuditLog"/>. Append-only; indexed by the entity it describes.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Action).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.PerformedBy).IsRequired();
        builder.Property(a => a.PerformedAt).IsRequired();
        builder.Property(a => a.OldValue);
        builder.Property(a => a.NewValue);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
