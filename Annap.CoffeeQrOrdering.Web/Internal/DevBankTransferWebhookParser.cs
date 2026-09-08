using System.Text.Json;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Dev/mock webhook parser. Phase 4B: add provider-specific parsers implementing <see cref="IPaymentConfirmationParser"/>.
/// </summary>
internal sealed class DevBankTransferWebhookParser : IPaymentConfirmationParser
{
    public const string ProviderName = "dev";

    public string Provider => ProviderName;

    public bool TryParse(string rawJson, out PaymentConfirmationInput? input, out string? error)
    {
        input = null;
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("amount", out var amountEl) || !amountEl.TryGetDecimal(out var amount))
            {
                error = "amount is required.";
                return false;
            }

            var memo = root.TryGetProperty("memo", out var memoEl) ? memoEl.GetString() : null;
            var provider = root.TryGetProperty("provider", out var providerEl)
                ? providerEl.GetString()
                : ProviderName;
            var transactionId = root.TryGetProperty("transactionId", out var txnEl) ? txnEl.GetString() : null;
            var receivedAt = root.TryGetProperty("receivedAtUtc", out var atEl)
                              && atEl.TryGetDateTimeOffset(out var parsedAt)
                ? parsedAt
                : DateTimeOffset.UtcNow;

            input = new PaymentConfirmationInput(
                string.IsNullOrWhiteSpace(provider) ? ProviderName : provider.Trim(),
                transactionId,
                amount,
                memo ?? "",
                receivedAt,
                root.TryGetProperty("accountNumber", out var acctEl) ? acctEl.GetString() : null,
                root.TryGetProperty("bankCode", out var bankEl) ? bankEl.GetString() : null,
                rawJson);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
