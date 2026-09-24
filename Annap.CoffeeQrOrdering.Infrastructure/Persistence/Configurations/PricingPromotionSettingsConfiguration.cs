using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class PricingPromotionSettingsConfiguration : IEntityTypeConfiguration<PricingPromotionSettings>
{
    /// <summary>Fixed id for the single promotion-settings row.</summary>
    public static readonly Guid SingletonId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");

    public void Configure(EntityTypeBuilder<PricingPromotionSettings> builder)
    {
        builder.ToTable("pricing_promotion_settings");
        builder.HasKey(x => x.Id);

        // Default values are written into the migration INSERT so the singleton
        // exists on first deploy in OFF state. We never set Percent above 100
        // in code, but the constraint keeps the column honest.
        builder.Property(x => x.Percent)
            .HasDefaultValue(10);

        // Enabled defaults to false; migration INSERT sets it explicitly.
        builder.Property(x => x.Enabled)
            .HasDefaultValue(false);
    }
}
