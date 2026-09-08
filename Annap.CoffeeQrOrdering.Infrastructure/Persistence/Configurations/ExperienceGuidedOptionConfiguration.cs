using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceGuidedOptionConfiguration : IEntityTypeConfiguration<ExperienceGuidedOption>
{
    public void Configure(EntityTypeBuilder<ExperienceGuidedOption> builder)
    {
        builder.ToTable("experience_guided_options");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalKey).HasMaxLength(96).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LabelEn).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1200);
        builder.Property(x => x.DescriptionEn).HasMaxLength(1200);
        builder.Property(x => x.Subline).HasMaxLength(400);
        builder.Property(x => x.SublineEn).HasMaxLength(400);
        builder.Property(x => x.SensoryProfileJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.MoodKey).HasMaxLength(120);
        builder.Property(x => x.RefinementKey).HasMaxLength(120);
        builder.Property(x => x.FlavorTagsJson).HasMaxLength(2000);
        builder.Property(x => x.WeightMultiplier).HasPrecision(10, 4).HasDefaultValue(1m);

        builder.HasIndex(x => new { x.QuestionId, x.ExternalKey }).IsUnique();

        builder.HasMany(x => x.Affinities)
            .WithOne(x => x.Option)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
