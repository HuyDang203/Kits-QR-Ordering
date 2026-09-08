namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class OrderCustomerNoteHelper
{
    public const int MaxLength = 300;

    /// <summary>Trims whitespace; returns null when empty. Rejects notes longer than <see cref="MaxLength"/>.</summary>
    public static string? Normalize(string? raw, out bool tooLong)
    {
        tooLong = false;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
        {
            tooLong = true;
            return null;
        }

        return trimmed;
    }
}
