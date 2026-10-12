using System.Globalization;
using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Single source of truth for selecting a menu item name in the active locale.
/// Used by:
///   - The guest-side Razor partial <c>_MenuEditorialDrinkCard</c>
///   - The catalog JSON embedded in <c>Pages/Menu/Index.cshtml</c>
///   - The drink detail client renderer
///   - The order submit path (snapshot the localized name on the OrderItem)
///
/// Rules:
///   - VI  -> always <see cref="MenuItem.Name"/>.
///   - EN  -> <see cref="MenuItem.NameEn"/> when it contains non-whitespace text,
///            otherwise falls back to <see cref="MenuItem.Name"/>.
///   - Any other / unknown locale -> <see cref="MenuItem.Name"/>.
///
/// Never returns null or whitespace; the entity invariant guarantees
/// <see cref="MenuItem.Name"/> is non-null, so a safe fallback always exists.
/// </summary>
public static class MenuItemNameLocalizer
{
    /// <summary>Locale code for Vietnamese (matches the i18n bundle <c>vi</c>).</summary>
    public const string VietnameseCode = "vi";

    /// <summary>Locale code for English (matches the i18n bundle <c>en</c>).</summary>
    public const string EnglishCode = "en";

    /// <summary>
    /// Returns the localized name for the given two-letter ISO language name
    /// (<c>vi</c> or <c>en</c>). Other values fall back to Vietnamese.
    /// </summary>
    public static string For(MenuItem item, string? twoLetterIsoLanguageName)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        return IsEnglish(twoLetterIsoLanguageName) && !string.IsNullOrWhiteSpace(item.NameEn)
            ? item.NameEn.Trim()
            : item.Name;
    }

    /// <summary>
    /// Convenience overload that reads the two-letter ISO language name from
    /// the supplied <see cref="CultureInfo"/>. Pass <c>CultureInfo.CurrentUICulture</c>
    /// for the request's resolved culture.
    /// </summary>
    public static string For(MenuItem item, CultureInfo? culture)
        => For(item, culture?.TwoLetterISOLanguageName);

    /// <summary>
    /// Resolves the two-letter ISO language name for the current request's UI culture.
    /// Falls back to <see cref="VietnameseCode"/> when no culture is available
    /// (e.g. background jobs that don't have an HTTP context).
    /// </summary>
    public static string CurrentLanguageCode()
    {
        try
        {
            return NormalizeLanguageCode(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }
        catch
        {
            return VietnameseCode;
        }
    }

    /// <summary>True if <paramref name="code"/> represents English (case-insensitive, trims).</summary>
    public static bool IsEnglish(string? code)
        => string.Equals(NormalizeLanguageCode(code), EnglishCode, StringComparison.Ordinal);

    private static string NormalizeLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return VietnameseCode;
        var s = code.Trim();
        return s.Length >= 2 ? s[..2].ToLowerInvariant() : VietnameseCode;
    }
}
