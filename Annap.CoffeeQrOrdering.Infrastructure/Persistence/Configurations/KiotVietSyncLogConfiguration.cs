using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class KiotVietSyncLogConfiguration : IEntityTypeConfiguration<KiotVietSyncLog>
{
    public void Configure(EntityTypeBuilder<KiotVietSyncLog> builder)
    {
        builder.ToTable("kiotviet_sync_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.SyncKind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsSuccess).IsRequired();
        builder.Property(x => x.ReferenceId).HasMaxLength(80);
        builder.Property(x => x.KiotVietReference).HasMaxLength(80);
        builder.Property(x => x.FailureReason).HasMaxLength(4000);
        builder.Property(x => x.DurationMs).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(4000);

        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("ix_kv_sync_logs_time")
            .IsDescending(true);
        builder.HasIndex(x => new { x.SyncKind, x.IsSuccess, x.OccurredAtUtc })
            .HasDatabaseName("ix_kv_sync_logs_kind_success_time");
    }
}
