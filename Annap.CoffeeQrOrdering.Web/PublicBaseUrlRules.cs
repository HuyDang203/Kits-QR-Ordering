using System.Net;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Shared rules for public guest/QR URLs (never encode localhost into production QR).</summary>
public static class PublicBaseUrlRules
{
    public static bool TryNormalizeAbsoluteHttpUrl(string? raw, out string normalized, out string? error)
    {
        normalized = "";
        error = null;
        var trimmed = (raw ?? "").Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            error = "Public base URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Public base URL must be an absolute http or https URL.";
            return false;
        }

        if (IsLoopbackHost(uri.Host))
        {
            error = "Public base URL must not use localhost or 127.0.0.1.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        var h = host.Trim();
        if (h.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || h.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || h.Equals("::1", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(h, out var ip) && IPAddress.IsLoopback(ip);
    }

    public static bool ConnectionStringUsesLoopbackHost(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Server", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                return IsLoopbackHost(value);
            }
        }

        return false;
    }
}
