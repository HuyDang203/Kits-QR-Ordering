using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceGuidedAffinityConfiguration : IEntityTypeConfiguration<ExperienceGuidedAffinity>
{
    public void Configure(EntityTypeBuilder<ExperienceGuidedAffinity> builder)
    {
        builder.ToTable("experience_guided_affinities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Weight).HasPrecision(10, 4).IsRequired();

        builder.HasIndex(x => new { x.OptionId, x.MenuItemId }).IsUnique();

        builder.HasOne(x => x.Option)
            .WithMany(x => x.Affinities)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
