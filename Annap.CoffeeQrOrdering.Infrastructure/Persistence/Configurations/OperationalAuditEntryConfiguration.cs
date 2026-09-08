using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class OperationalAuditEntryConfiguration : IEntityTypeConfiguration<OperationalAuditEntry>
{
    public void Configure(EntityTypeBuilder<OperationalAuditEntry> builder)
    {
        builder.ToTable("operational_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionKind).HasMaxLength(96).IsRequired();
        builder.Property(x => x.Actor).HasMaxLength(160);
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.OrderId);
    }
}
