namespace Annap.CoffeeQrOrdering.Domain;

/// <summary>Guest serving temperature for coffee drinks that support Hot/Iced.</summary>
public static class DrinkServingTemperature
{
    public const string Hot = "Hot";
    public const string Iced = "Iced";

    public static bool IsValid(string? value) =>
        string.Equals(value, Hot, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Iced, StringComparison.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (string.Equals(value, Hot, StringComparison.OrdinalIgnoreCase)) return Hot;
        if (string.Equals(value, Iced, StringComparison.OrdinalIgnoreCase)) return Iced;
        return null;
    }

    /// <summary>
    /// Coffee-family drinks that are not already a dedicated iced/hot SKU.
    /// Display-only eligibility — no separate option entity.
    /// </summary>
    public static bool Supports(string? categoryName, string? itemName, string? itemType = null)
    {
        var cat = (categoryName ?? string.Empty).Trim();
        if (cat is not (
            "Espresso"
            or "Coffee"
            or "CAFE"
            or "Specialty Coffee"
            or "Vietnamese Coffee"
            or "Signature"))
        {
            return false;
        }

        var blob = $"{itemName} {itemType}".ToLowerInvariant();
        if (blob.Contains("đá", StringComparison.Ordinal)) return false;
        if (blob.Contains("iced", StringComparison.Ordinal)) return false;
        if (blob.Contains("cold brew", StringComparison.Ordinal) || blob.Contains("coldbrew", StringComparison.Ordinal))
            return false;

        return true;
    }

    public static string DisplayVi(string? temperature) =>
        Normalize(temperature) switch
        {
            Hot => "Nóng",
            Iced => "Đá",
            _ => ""
        };

    public static string DisplayEn(string? temperature) =>
        Normalize(temperature) switch
        {
            Hot => "Hot",
            Iced => "Iced",
            _ => ""
        };
}
