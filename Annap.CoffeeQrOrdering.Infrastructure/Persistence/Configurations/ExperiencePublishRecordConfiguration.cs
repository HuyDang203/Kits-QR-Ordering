using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperiencePublishRecordConfiguration : IEntityTypeConfiguration<ExperiencePublishRecord>
{
    public void Configure(EntityTypeBuilder<ExperiencePublishRecord> builder)
    {
        builder.ToTable("experience_publish_records");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Snapshot)
            .WithMany()
            .HasForeignKey(x => x.SnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
