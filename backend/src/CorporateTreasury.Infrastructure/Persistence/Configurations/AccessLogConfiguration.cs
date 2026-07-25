using CorporateTreasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateTreasury.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AccessLog"/>. Append-only; indexed for the scoped/filtered reads.</summary>
public sealed class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.ToTable("AccessLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId);
        builder.Property(a => a.EventType).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.Timestamp).IsRequired();

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
    }
}
