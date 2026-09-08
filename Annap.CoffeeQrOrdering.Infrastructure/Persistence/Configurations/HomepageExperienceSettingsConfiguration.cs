using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class HomepageExperienceSettingsConfiguration : IEntityTypeConfiguration<HomepageExperienceSettings>
{
    public static readonly Guid SingletonId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8");

    public void Configure(EntityTypeBuilder<HomepageExperienceSettings> builder)
    {
        builder.ToTable("homepage_experience_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsGroupEnabled).HasDefaultValue(true);
        builder.Property(x => x.IsSoloEnabled).HasDefaultValue(true);
        builder.Property(x => x.IsSommelierEnabled).HasDefaultValue(true);
    }
}
