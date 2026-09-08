using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class KiotVietOutboxMessageConfiguration : IEntityTypeConfiguration<KiotVietOutboxMessage>
{
    public void Configure(EntityTypeBuilder<KiotVietOutboxMessage> builder)
    {
        builder.ToTable("kiotviet_outbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();
        builder.Property(x => x.KiotVietOrderId).HasMaxLength(64);
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("ix_kv_outbox_order_id");

        builder.HasIndex(x => new { x.Status, x.NextRetryAtUtc })
            .HasDatabaseName("ix_kv_outbox_status_retry")
            .HasFilter("\"Status\" IN (0, 3)");

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
