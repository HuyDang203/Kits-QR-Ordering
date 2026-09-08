namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Provider-agnostic bank transfer confirmation input.
/// Future providers map webhook payloads to this shape via <see cref="IPaymentConfirmationParser"/>.
/// </summary>
internal sealed record PaymentConfirmationInput(
    string Provider,
    string? ProviderTransactionId,
    decimal Amount,
    string Memo,
    DateTimeOffset ReceivedAtUtc,
    string? AccountNumber = null,
    string? BankCode = null,
    string? RawPayloadJson = null);

internal sealed record BankTransferConfirmationResult(
    string Status,
    Guid? OrderId = null,
    string? BillNumber = null,
    decimal? Amount = null,
    string? Reason = null,
    Guid? MatchedOrderId = null,
    Guid? ConfirmationId = null);

/// <summary>
/// Maps a provider-specific webhook body to <see cref="PaymentConfirmationInput"/>.
/// Phase 4B: implement per provider (e.g. VietQR, bank CASA feed).
/// </summary>
internal interface IPaymentConfirmationParser
{
    string Provider { get; }

    bool TryParse(string rawJson, out PaymentConfirmationInput? input, out string? error);
}
