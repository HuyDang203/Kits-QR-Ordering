using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Blocker S1 regression proof: rate-limit partitions come from the transport-level client
/// address, and forwarded headers are honored only from trusted proxy networks, so a spoofed
/// X-Forwarded-For can no longer mint fresh rate-limit buckets.
/// </summary>
public sealed class RateLimitSpoofingTests
{
    [Fact]
    public void Partition_ignores_spoofed_forwarded_header_when_socket_peer_known()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        http.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 198.51.100.2";

        Assert.Equal("203.0.113.7", RateLimitClientKey.Resolve(http));
    }

    [Fact]
    public void Partition_changes_only_with_real_client_address()
    {
        var first = new DefaultHttpContext();
        first.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        first.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

        var second = new DefaultHttpContext();
        second.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        second.Request.Headers["X-Forwarded-For"] = "10.0.0.2";

        Assert.Equal(RateLimitClientKey.Resolve(first), RateLimitClientKey.Resolve(second));
    }

    [Fact]
    public void Forwarded_headers_consume_exactly_one_proxy_hop()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxyNetworks.Apply(options);

        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.Empty(options.KnownProxies);
        Assert.NotEmpty(options.KnownNetworks);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.20.30.40", true)]
    [InlineData("172.18.0.2", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("100.64.10.20", true)]
    [InlineData("::1", true)]
    [InlineData("fdaa::1", true)]
    [InlineData("::ffff:10.0.0.5", true)]
    [InlineData("203.0.113.7", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void Only_private_and_loopback_peers_are_trusted_proxies(string address, bool trusted)
    {
        Assert.Equal(trusted, TrustedProxyNetworks.IsTrustedProxy(IPAddress.Parse(address)));
    }

    [Fact]
    public void Public_direct_client_is_not_in_any_known_network()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxyNetworks.Apply(options);

        var attacker = IPAddress.Parse("203.0.113.7");
        Assert.DoesNotContain(options.KnownNetworks, n => n.Contains(attacker));
    }
}
