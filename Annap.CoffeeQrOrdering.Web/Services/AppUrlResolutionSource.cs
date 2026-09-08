namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Which layer supplied the public base URL used for QR generation.</summary>
public enum AppUrlResolutionSource
{
    /// <summary><see cref="Domain.Entities.AppNetworkSettings.PublicBaseUrlOverride"/> (admin /admin/system/network).</summary>
    DatabaseOverride,

    /// <summary>appsettings / env <c>AppUrl:PublicBaseUrl</c> (<c>AppUrl__PublicBaseUrl</c>).</summary>
    AppUrlPublicBaseUrl,

    /// <summary>Current HTTP request scheme + host (no override or config).</summary>
    RequestHost,

    /// <summary>No override, config, or HTTP request available (startup-only edge case).</summary>
    Unresolved
}
