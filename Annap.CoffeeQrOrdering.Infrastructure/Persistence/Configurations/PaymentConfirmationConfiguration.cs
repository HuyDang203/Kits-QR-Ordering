using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfirmationConfiguration : IEntityTypeConfiguration<PaymentConfirmation>
{
    public void Configure(EntityTypeBuilder<PaymentConfirmation> builder)
    {
        builder.ToTable("payment_confirmations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(128);
        builder.Property(x => x.Memo).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(64);
        builder.Property(x => x.BankCode).HasMaxLength(32);
        builder.Property(x => x.RawPayloadJson).HasMaxLength(8000);
        builder.Property(x => x.MatchStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.Amount).HasPrecision(12, 2).IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
            .IsUnique()
            .HasFilter("\"ProviderTransactionId\" IS NOT NULL");

        builder.HasIndex(x => x.Memo);
        builder.HasIndex(x => x.MatchedOrderId);
        builder.HasIndex(x => x.ReceivedAtUtc);
    }
}
