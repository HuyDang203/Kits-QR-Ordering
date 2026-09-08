using Annap.CoffeeQrOrdering.Domain.Entities;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;



namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;



public sealed class ShiftCloseConfiguration : IEntityTypeConfiguration<ShiftClose>

{

    public void Configure(EntityTypeBuilder<ShiftClose> builder)

    {

        builder.ToTable("shift_closes");

        builder.HasKey(x => x.Id);



        builder.Property(x => x.ClosedBy).HasMaxLength(120).IsRequired();

        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.Property(x => x.TotalGrossAmount).HasPrecision(18, 2);

        builder.Property(x => x.CashOrCardAmount).HasPrecision(18, 2);

        builder.Property(x => x.BankTransferAmount).HasPrecision(18, 2);

        builder.Property(x => x.UnknownPaymentAmount).HasPrecision(18, 2);



        builder.HasIndex(x => x.ClosedAtUtc);

        builder.HasIndex(x => x.ClosedByAccountId);

        // Every close opens where the previous close ended, so two rows with the same
        // OpenedAtUtc are always a duplicated window. Unique index makes the race lose at the DB.
        builder.HasIndex(x => x.OpenedAtUtc).IsUnique();

    }

}


