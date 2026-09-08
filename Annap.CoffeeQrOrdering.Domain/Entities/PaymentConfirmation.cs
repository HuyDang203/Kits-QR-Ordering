namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Received bank transfer confirmation from a provider webhook (audit + matching).</summary>
public sealed class PaymentConfirmation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Provider { get; set; } = null!;

    public string? ProviderTransactionId { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public decimal Amount { get; set; }

    public string Memo { get; set; } = null!;

    public string? AccountNumber { get; set; }

    public string? BankCode { get; set; }

    public string? RawPayloadJson { get; set; }

    public Guid? MatchedOrderId { get; set; }

    public string MatchStatus { get; set; } = PaymentConfirmationMatchStatus.Unmatched;

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string? Notes { get; set; }
}

public static class PaymentConfirmationMatchStatus
{
    public const string Unmatched = "Unmatched";
    public const string Matched = "Matched";
    public const string Duplicate = "Duplicate";
    public const string AmountMismatch = "AmountMismatch";
    public const string MemoMissing = "MemoMissing";
    public const string OrderAlreadyPaid = "OrderAlreadyPaid";
    public const string Ignored = "Ignored";
}
