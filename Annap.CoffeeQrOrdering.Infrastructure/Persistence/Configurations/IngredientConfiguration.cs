using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CurrentStock).HasPrecision(14, 4);
        builder.Property(x => x.LowStockThreshold).HasPrecision(14, 4);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
    }
}
