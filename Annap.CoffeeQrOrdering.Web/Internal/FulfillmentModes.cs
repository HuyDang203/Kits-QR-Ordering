namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Canonical guest fulfillment-mode values persisted on <see cref="Domain.Entities.Order.FulfillmentMode"/>.
/// </summary>
internal static class FulfillmentModes
{
    public const string DineIn = "DineIn";
    public const string Takeaway = "Takeaway";

    public static string? Normalize(string? raw) => NormalizeOrDefault(raw, requireValue: false);

    /// <summary>
    /// Normalizes the raw value. Returns null when <paramref name="requireValue"/> is false
    /// and the input is null/empty; throws when <paramref name="requireValue"/> is true
    /// and no valid value can be produced.
    /// </summary>
    public static string? NormalizeOrDefault(string? raw, bool requireValue = true)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (requireValue)
                return null; // caller decides how to handle
            return null;
        }

        var t = raw.Trim();
        if (t.Equals(DineIn, StringComparison.OrdinalIgnoreCase))
            return DineIn;
        if (t.Equals(Takeaway, StringComparison.OrdinalIgnoreCase))
            return Takeaway;

        return null;
    }

    public static (string Vi, string En) Labels(string? mode) => mode switch
    {
        DineIn => ("Uống tại bàn", "Dine in"),
        Takeaway => ("Take away", "Take away"),
        _ => ("Không xác định", "Unknown")
    };

    public static string SubmittedBadgeVi(string? mode) => mode switch
    {
        DineIn => "Uống tại bàn",
        Takeaway => "Take away",
        _ => "Không xác định"
    };
}
