namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Guest-facing category labels. Internal MenuCategory.Name stays unchanged.</summary>
public static class GuestCategoryDisplay
{
    public static string For(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return categoryName ?? "";
        return categoryName.Trim() switch
        {
            "Signature" => "Good Things",
            _ => categoryName.Trim()
        };
    }
}
