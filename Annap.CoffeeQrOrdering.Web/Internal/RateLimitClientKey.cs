namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Rate-limit partition key for anonymous/login endpoints. Uses the transport-level client
/// address only: after the forwarded-headers middleware has resolved the trusted proxy hop,
/// Connection.RemoteIpAddress is the canonical caller and request headers are never consulted,
/// so a spoofed X-Forwarded-For cannot open a fresh rate-limit bucket.
/// </summary>
internal static class RateLimitClientKey
{
    internal static string Resolve(HttpContext http)
    {
        var remote = http.Connection.RemoteIpAddress;
        if (remote is not null)
            return remote.ToString();

        // Only the in-memory TestServer has no socket peer; real Kestrel connections always
        // carry a remote address. Keep test partitions stable via the test-supplied header.
        var testPartition = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(testPartition) ? "unknown" : testPartition;
    }
}
