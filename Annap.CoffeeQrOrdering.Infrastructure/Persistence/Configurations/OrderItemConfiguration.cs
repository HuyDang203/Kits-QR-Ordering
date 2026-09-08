using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CustomerNote).HasMaxLength(200);
        builder.Property(x => x.Temperature).HasMaxLength(16);
        builder.Property(x => x.MenuItemName).HasMaxLength(200);
        builder.Property(x => x.PreparedQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.PreparedAtUtc);
        builder.Property(x => x.PreparedBy).HasMaxLength(120);
        builder.Property(x => x.PreparedByAccountId);

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.MenuItemId);

        builder.HasOne(x => x.MenuItem)
            .WithMany()
            .HasForeignKey(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("CK_order_items_Quantity_Positive", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_order_items_PreparedQuantity_Range", "\"PreparedQuantity\" >= 0 AND \"PreparedQuantity\" <= \"Quantity\"");
        });
    }
}
