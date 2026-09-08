using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceDiscoverySettingsConfiguration : IEntityTypeConfiguration<ExperienceDiscoverySettings>
{
    public static readonly Guid SingletonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");

    public void Configure(EntityTypeBuilder<ExperienceDiscoverySettings> builder)
    {
        builder.ToTable("experience_discovery_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourierMoodCopy).HasMaxLength(1200);
        builder.Property(x => x.FatigueCopyEvenLeg).HasMaxLength(600);
        builder.Property(x => x.FatigueCopyOddLeg).HasMaxLength(600);
        builder.Property(x => x.RerollPacingJson).HasMaxLength(8000);
        builder.Property(x => x.RevealCopyNotes).HasMaxLength(2000);
        builder.Property(x => x.LetterRoomContentJson).HasMaxLength(16000);
        builder.Property(x => x.AdventureTone).HasDefaultValue(3);
        builder.Property(x => x.AllowSeasonalCups).HasDefaultValue(true);
        builder.Property(x => x.PreferSignaturesFirst).HasDefaultValue(true);
        builder.Property(x => x.AllowRerolls).HasDefaultValue(true);
    }
}
