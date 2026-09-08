using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceSnapshotConfiguration : IEntityTypeConfiguration<ExperienceSnapshot>
{
    public void Configure(EntityTypeBuilder<ExperienceSnapshot> builder)
    {
        builder.ToTable("experience_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.HouseNote).HasMaxLength(400);
        builder.Property(x => x.Kind).HasDefaultValue((byte)0);
        builder.HasIndex(x => new { x.Kind, x.CreatedAtUtc });
    }
}
