using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class VenueTableConfiguration : IEntityTypeConfiguration<VenueTable>
{
    public void Configure(EntityTypeBuilder<VenueTable> builder)
    {
        builder.ToTable("venue_tables");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VenueCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DisplayCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PublicSlug).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayLabel).HasMaxLength(120);
        builder.Property(x => x.KiotVietTableId).HasMaxLength(64);
        builder.Property(x => x.KiotVietBranchId);

        builder.HasIndex(x => x.PublicSlug).IsUnique();
        builder.HasIndex(x => new { x.VenueCode, x.DisplayCode }).IsUnique();
    }
}
