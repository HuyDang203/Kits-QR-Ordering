using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class MenuItemIngredientConfiguration : IEntityTypeConfiguration<MenuItemIngredient>
{
    public void Configure(EntityTypeBuilder<MenuItemIngredient> builder)
    {
        builder.ToTable("menu_item_ingredients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityRequired).HasPrecision(14, 4).IsRequired();
        builder.HasIndex(x => new { x.MenuItemId, x.IngredientId }).IsUnique();
        builder.HasOne(x => x.MenuItem)
            .WithMany(m => m.RecipeLines)
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Ingredient)
            .WithMany(i => i.MenuLinks)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
