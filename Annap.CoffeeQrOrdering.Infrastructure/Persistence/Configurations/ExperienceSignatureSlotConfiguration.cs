using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceSignatureSlotConfiguration : IEntityTypeConfiguration<ExperienceSignatureSlot>
{
    public void Configure(EntityTypeBuilder<ExperienceSignatureSlot> builder)
    {
        builder.ToTable("experience_signature_slots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.EditorialKicker).HasMaxLength(240);
        builder.Property(x => x.EditorialBody).HasMaxLength(1200);

        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.MenuItemId);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
