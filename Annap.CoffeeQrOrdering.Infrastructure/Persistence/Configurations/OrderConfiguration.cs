using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
            t.HasCheckConstraint("CK_orders_Status_Valid", "\"Status\" IN (0, 1, 2, 3, 4, 5, 6, 7)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TableCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.GuestSessionToken).HasMaxLength(80);
        builder.Property(x => x.SubmitIdempotencyKey).HasMaxLength(120);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.StatusChangedAtUtc);
        builder.Property(x => x.PaidAtUtc);
        builder.Property(x => x.PaymentConfirmedBy).HasMaxLength(120);
        builder.Property(x => x.PaymentConfirmedByAccountId);
        builder.Property(x => x.PaymentMethod).HasMaxLength(40);
        builder.Property(x => x.CustomerNote).HasMaxLength(300);
        builder.Property(x => x.BillNumber).HasMaxLength(24);
        builder.Property(x => x.CompletedAtUtc);
        builder.Property(x => x.CompletedBy).HasMaxLength(120);
        builder.Property(x => x.CompletedByAccountId);
        builder.Property(x => x.BrewingOwnerStaffName).HasMaxLength(120);
        builder.Property(x => x.ServingOwnerStaffName).HasMaxLength(120);

        builder.HasOne(x => x.VenueTable)
            .WithMany()
            .HasForeignKey(x => x.VenueTableId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId);

        // Staff board reads orders grouped by Status and sorted by CreatedAtUtc.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.TableCode);
        builder.HasIndex(x => x.GuestSessionToken).IsUnique();
        builder.HasIndex(x => x.SubmitIdempotencyKey)
            .IsUnique()
            .HasFilter("\"SubmitIdempotencyKey\" IS NOT NULL");
    }
}

