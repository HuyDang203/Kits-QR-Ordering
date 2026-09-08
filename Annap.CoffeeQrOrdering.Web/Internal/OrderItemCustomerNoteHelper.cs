namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class OrderItemCustomerNoteHelper
{
    public const int MaxLength = 200;

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
