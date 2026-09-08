using System.Globalization;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Canonical VND display: <c>125.000 ₫</c> (vi-VN grouping, no decimals, suffix).</summary>
internal static class VndMoneyFormatter
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string Format(decimal amount)
    {
        var rounded = Math.Round(amount, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("N0", ViCulture)} ₫";
    }

    public static string Format(long amount) =>
        $"{amount.ToString("N0", ViCulture)} ₫";
}
