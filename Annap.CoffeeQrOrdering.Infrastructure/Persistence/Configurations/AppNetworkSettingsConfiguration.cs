using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class AppNetworkSettingsConfiguration : IEntityTypeConfiguration<AppNetworkSettings>
{
    public void Configure(EntityTypeBuilder<AppNetworkSettings> builder)
    {
        builder.ToTable("app_network_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicBaseUrlOverride).HasMaxLength(2000);
    }
}
