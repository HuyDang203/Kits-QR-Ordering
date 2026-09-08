using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using ProxyNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>
/// Forwarded-header trust policy: only reverse proxies on loopback or private/carrier-grade
/// networks (Render's edge, Cloudflare tunnel peers, docker-compose) may supply
/// X-Forwarded-For / X-Forwarded-Proto. A client connecting straight to Kestrel from a public
/// address gets its forwarded headers ignored, so it cannot spoof another client identity.
/// </summary>
internal static class TrustedProxyNetworks
{
    private static readonly (IPAddress Prefix, int PrefixLength)[] Networks =
    [
        (IPAddress.Parse("127.0.0.0"), 8),      // IPv4 loopback
        (IPAddress.Parse("10.0.0.0"), 8),       // RFC 1918
        (IPAddress.Parse("172.16.0.0"), 12),    // RFC 1918 (docker-compose default bridge)
        (IPAddress.Parse("192.168.0.0"), 16),   // RFC 1918
        (IPAddress.Parse("100.64.0.0"), 10),    // RFC 6598 carrier-grade NAT (Render internal)
        (IPAddress.Parse("::1"), 128),          // IPv6 loopback
        (IPAddress.Parse("fc00::"), 7)          // IPv6 unique local
    ];

    internal static void Apply(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Consume exactly one hop: the entry appended by our own edge proxy. Client-supplied
        // (leftmost) entries beyond that hop are never trusted.
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var (prefix, length) in Networks)
            options.KnownNetworks.Add(new ProxyNetwork(prefix, length));
    }

    internal static bool IsTrustedProxy(IPAddress address)
    {
        var candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        foreach (var (prefix, length) in Networks)
        {
            if (prefix.AddressFamily == candidate.AddressFamily
                && new ProxyNetwork(prefix, length).Contains(candidate))
                return true;
        }

        return false;
    }
}
