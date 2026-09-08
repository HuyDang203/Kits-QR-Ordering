using Annap.CoffeeQrOrdering.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal sealed record SommelierSuggestBody(
    Guid? SessionId,
    string? MoodKey,
    string? MoodLabel,
    string? RefinementKey,
    string? Prompt,
    string? Refinement,
    string? BeverageFamily,
    /// <summary>Guest UI language: <c>en</c> or <c>vi</c>.</summary>
    string? Language);

internal static class OrderSubmitLimits
{
    public const int MaxLineItems = 20;
}

internal sealed record CreateOrderRequest(Guid VenueTableId, List<CreateOrderItemRequest> Items)
{
    public List<CreateOrderItemRequest> Items { get; init; } = Items ?? [];

    public string? IdempotencyKey { get; init; }

    /// <summary>Cash, Card, BankTransfer, or legacy CashOrCardAtCounter.</summary>
    public string? PaymentMethod { get; init; }
}

internal sealed record CreateOrderItemRequest(
    Guid MenuItemId,
    int Quantity,
    string? Notes,
    string? CustomerNote,
    string? Temperature = null);

internal sealed record StaffOrderStatusPatchRequest(string? StaffStatus);

internal sealed record StaffOrderOwnershipPatchRequest(bool? ClaimBrewing, bool? ReleaseBrewing, bool? ClaimServing, bool? ReleaseServing);

internal sealed record SommelierFeedbackRequest(Guid SessionId, Guid MenuItemId, string? Outcome, string? MoodKey, string? RefinementKey);

internal static class GuestSessionTokens
{
    public static string Create()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Matches(string? storedToken, string? suppliedToken)
    {
        if (string.IsNullOrEmpty(storedToken))
            return false;
        if (string.IsNullOrWhiteSpace(suppliedToken))
            return false;
        var a = Encoding.UTF8.GetBytes(storedToken);
        var b = Encoding.UTF8.GetBytes(suppliedToken.Trim());
        if (a.Length != b.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
