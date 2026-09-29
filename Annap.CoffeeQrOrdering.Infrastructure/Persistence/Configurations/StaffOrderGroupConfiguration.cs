using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class StaffOrderGroupConfiguration : IEntityTypeConfiguration<StaffOrderGroup>
{
    public void Configure(EntityTypeBuilder<StaffOrderGroup> builder)
    {
        builder.ToTable("staff_order_groups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedByStaffName).HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StaffOrderGroupMemberConfiguration : IEntityTypeConfiguration<StaffOrderGroupMember>
{
    public void Configure(EntityTypeBuilder<StaffOrderGroupMember> builder)
    {
        builder.ToTable("staff_order_group_members");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AddedByStaffName).HasMaxLength(120).IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // One membership row per (Group, Order). Re-adding the same Order to
        // the same group is enforced by the application layer (and by the
        // unique index) so the staff board never displays duplicated lines.
        builder.HasIndex(x => new { x.GroupId, x.OrderId }).IsUnique();
        // A single Order can belong to at most one active group at a time.
        // Membership is the only thing the index protects; the underlying
        // Order row is never modified.
        builder.HasIndex(x => x.OrderId).IsUnique();
    }
}