using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CatalogKey).HasMaxLength(160);
        builder.Property(x => x.ItemType).HasMaxLength(80);
        builder.Property(x => x.Subtitle).HasMaxLength(240);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Price).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.DisplaySortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsFeatured).HasDefaultValue(false);
        builder.Property(x => x.IsSignature).HasDefaultValue(false);
        builder.Property(x => x.IsArchived).HasDefaultValue(false);
        builder.Property(x => x.ImageUrl).HasMaxLength(2000);
        builder.Property(x => x.DetailPosterImagePath).HasMaxLength(2000);

        builder.HasIndex(x => x.CategoryId);
    }
}