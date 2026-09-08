using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Web.Resources;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Builds nested JSON i18n bundles from embedded .resx (single source of truth).</summary>
internal sealed class I18nBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly ResourceManager _resourceManager = new(
        "Annap.CoffeeQrOrdering.Web.Resources.SharedResources",
        typeof(SharedResources).Assembly);

    public string GetBundleJson(string lang)
    {
        var culture = lang.Equals("vi", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("vi-VN")
            : CultureInfo.GetCultureInfo("en-US");

        var flat = ReadFlatStrings(culture);
        var nested = NestFlatKeys(flat);
        nested["lang"] = lang.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
        return JsonSerializer.Serialize(nested, JsonOptions);
    }

    internal IReadOnlyDictionary<string, string> ReadFlatStrings(CultureInfo culture)
    {
        var set = _resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: true);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (set is null)
            return result;

        foreach (System.Collections.DictionaryEntry entry in set)
        {
            if (entry.Key is not string key || string.IsNullOrWhiteSpace(key))
                continue;
            if (key is "resmimetype" or "version" or "reader" or "writer")
                continue;
            result[key] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }

    internal static Dictionary<string, object> NestFlatKeys(IReadOnlyDictionary<string, string> flat)
    {
        var root = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in flat)
        {
            var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                continue;

            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!current.TryGetValue(parts[i], out var nextObj))
                {
                    nextObj = new Dictionary<string, object>(StringComparer.Ordinal);
                    current[parts[i]] = nextObj;
                }

                current = (Dictionary<string, object>)nextObj;
            }

            current[parts[^1]] = value;
        }

        return root;
    }
}
