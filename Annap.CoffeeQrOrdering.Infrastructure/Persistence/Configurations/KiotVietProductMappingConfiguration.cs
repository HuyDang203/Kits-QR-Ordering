using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class KiotVietProductMappingConfiguration : IEntityTypeConfiguration<KiotVietProductMapping>
{
    public void Configure(EntityTypeBuilder<KiotVietProductMapping> builder)
    {
        builder.ToTable("kiotviet_product_mappings");
        builder.HasKey(x => x.MenuItemId);
        builder.Property(x => x.MenuItemId).ValueGeneratedNever();

        builder.Property(x => x.KiotVietProductCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.KiotVietProductName).HasMaxLength(512);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastSyncedAtUtc).IsRequired();
        builder.Property(x => x.SyncNote).HasMaxLength(1000);

        builder.HasIndex(x => x.KiotVietProductCode)
            .IsUnique()
            .HasDatabaseName("ix_kv_product_code");

        builder.HasOne(x => x.MenuItem)
            .WithOne()
            .HasForeignKey<KiotVietProductMapping>(x => x.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
