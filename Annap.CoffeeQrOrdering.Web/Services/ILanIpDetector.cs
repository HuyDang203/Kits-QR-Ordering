namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Detects a sensible WiFi / LAN IPv4 for Development QR demos (excludes Hyper-V, WSL, Docker, loopback).</summary>
public interface ILanIpDetector
{
    /// <summary>Returns the best IPv4 string (no brackets), or null if none found.</summary>
    string? TryGetPreferredLanIPv4();

    /// <summary><c>http://{ip}:{port}</c> using <see cref="TryGetPreferredLanIPv4"/>, or null.</summary>
    string? TryGetPreferredLanBaseUrl(int port = 8080);
}
