using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class StaffAccountConfiguration : IEntityTypeConfiguration<StaffAccount>
{
    public void Configure(EntityTypeBuilder<StaffAccount> builder)
    {
        builder.ToTable("staff_accounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(120);

        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
