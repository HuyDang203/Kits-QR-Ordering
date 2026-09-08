using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Application;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Deterministic slug / catalog-key generation for Annap drink names (Vietnamese-safe).</summary>
public static class DrinkSlugGenerator
{
    private static readonly Regex NonSlug = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CategoryPrefixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Signature"] = "sig",
            ["Specialty Coffee"] = "sc",
            ["Espresso"] = "esp",
            ["Tea"] = "tea",
            ["Smoothie"] = "sm",
            ["Juice"] = "juice",
            ["Cold Brew"] = "cb",
            ["Vietnamese Coffee"] = "vnc",
            [KitsMenuCatalog.Categories.Banh] = "banh"
        };

    public static string ToCatalogKey(string? category, string? drinkName)
    {
        var prefix = CategoryPrefix(category);
        var slug = ToSlug(drinkName);
        return string.IsNullOrEmpty(slug) ? prefix : $"{prefix}-{slug}";
    }

    public static string CategoryPrefix(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "annap";
        var t = category.Trim();
        if (CategoryPrefixes.TryGetValue(t, out var p))
            return p;
        var slug = ToSlug(t);
        return string.IsNullOrEmpty(slug) ? "annap" : slug;
    }

    public static string ToSlug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var t = Whitespace.Replace(text.Trim(), " ");
        t = RemoveDiacritics(t).ToLowerInvariant();
        t = NonSlug.Replace(t, "-").Trim('-');
        while (t.Contains("--", StringComparison.Ordinal))
            t = t.Replace("--", "-", StringComparison.Ordinal);
        return t;
    }

    public static string AccentColorForCategory(string? category) => AccentFromKey(category);

    public static string AccentFromKey(string? key)
    {
        ReadOnlySpan<string> palette =
        [
            "#e8c76b", "#d9b56a", "#c9a87c", "#b5c9a8", "#d4a574", "#c4b8e8"
        ];
        var k = string.IsNullOrWhiteSpace(key) ? "annap" : key.Trim();
        var idx = Math.Abs(k.GetHashCode(StringComparison.Ordinal)) % palette.Length;
        return palette[idx];
    }

    public static IReadOnlyList<string> SignificantTokens(string? drinkName)
    {
        var slug = ToSlug(drinkName);
        if (string.IsNullOrEmpty(slug))
            return [];
        return slug.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1 || t is "o" or "x")
            .ToArray();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }
}
