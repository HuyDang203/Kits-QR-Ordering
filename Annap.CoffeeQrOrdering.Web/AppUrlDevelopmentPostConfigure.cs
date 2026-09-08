using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Development-only: if <see cref="AppUrlOptions.PublicBaseUrl"/> is empty or points at localhost, 127.0.0.1, or 172.20.x (WSL/Hyper-V),
/// set or replace host with <see cref="ILanIpDetector"/> so QR and runtime config match real WiFi.
/// </summary>
public sealed class AppUrlDevelopmentPostConfigure(
    IWebHostEnvironment environment,
    ILanIpDetector lanIpDetector,
    IConfiguration configuration,
    ILogger<AppUrlDevelopmentPostConfigure> logger) : IPostConfigureOptions<AppUrlOptions>
{
    public void PostConfigure(string? name, AppUrlOptions options)
    {
        if (!environment.IsDevelopment())
            return;

        var detectedIp = lanIpDetector.TryGetPreferredLanIPv4();
        if (TryParseHttpAuthority(options.PublicBaseUrl, out var configuredHost, out _)
            && !string.IsNullOrEmpty(detectedIp)
            && ShouldReplaceHost(configuredHost)
            && !configuredHost.Equals(detectedIp, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "[LAN] Warning: using virtual adapter IP instead of physical LAN IP. Configured host was {ConfiguredHost}; detected LAN is {DetectedIp}.",
                configuredHost,
                detectedIp);
        }

        if (string.IsNullOrEmpty(detectedIp))
        {
            logger.LogDebug("AppUrl post-configure: no WiFi/LAN IPv4 detected; leaving PublicBaseUrl as configured.");
            return;
        }

        var defaultPort = ResolveDevListenPort(configuration);

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            options.PublicBaseUrl = $"http://{detectedIp}:{defaultPort}";
            logger.LogInformation("AppUrl: PublicBaseUrl was empty; set to detected WiFi/LAN → {Url}", options.PublicBaseUrl);
            return;
        }

        if (!TryParseHttpAuthority(options.PublicBaseUrl, out var host, out var port))
            return;

        if (!ShouldReplaceHost(host))
            return;

        var newPort = port > 0 ? port : defaultPort;
        var old = options.PublicBaseUrl;
        options.PublicBaseUrl = $"http://{detectedIp}:{newPort}";
        logger.LogInformation(
            "AppUrl: PublicBaseUrl host was {OldHost}; replaced with detected WiFi/LAN IP → {NewUrl} (was {OldUrl})",
            host,
            options.PublicBaseUrl,
            old);
    }

    private static int ResolveDevListenPort(IConfiguration configuration)
    {
        var urls = configuration["urls"] ?? configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
        foreach (var part in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var u) && u.Port > 0)
                return u.Port;
        }

        return 8080;
    }

    private static bool ShouldReplaceHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        if (host.Equals("127.0.0.1", StringComparison.Ordinal))
            return true;
        if (host.StartsWith("172.20.", StringComparison.Ordinal))
            return true;
        return false;
    }

    private static bool TryParseHttpAuthority(string? baseUrl, out string host, out int port)
    {
        host = "";
        port = 0;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        host = uri.Host;
        port = uri.Port;
        return true;
    }
}
