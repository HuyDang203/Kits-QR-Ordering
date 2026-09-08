using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceGroupSettingsConfiguration : IEntityTypeConfiguration<ExperienceGroupSettings>
{
    public static readonly Guid SingletonId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

    public void Configure(EntityTypeBuilder<ExperienceGroupSettings> builder)
    {
        builder.ToTable("experience_group_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArrivalKicker).HasMaxLength(240);
        builder.Property(x => x.GuestCountPrompt).HasMaxLength(400).IsRequired();
        builder.Property(x => x.GuestCountLead).HasMaxLength(800);
        builder.Property(x => x.GuestTabsIntro).HasMaxLength(800);
        builder.Property(x => x.GuestDoneHint).HasMaxLength(800);
        builder.Property(x => x.SummaryHeadline).HasMaxLength(400).IsRequired();
        builder.Property(x => x.SummaryLead).HasMaxLength(1200);
        builder.Property(x => x.HospitalityClosing).HasMaxLength(1200);
        builder.Property(x => x.MinGuests).HasDefaultValue(1);
        builder.Property(x => x.MaxGuests).HasDefaultValue(8);
    }
}
