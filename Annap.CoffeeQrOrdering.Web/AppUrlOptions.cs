namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Public site URL for QR codes and absolute external links (appsettings:AppUrl). Not used for in-app API or SignalR routing.</summary>
public sealed class AppUrlOptions
{
    public const string SectionName = "AppUrl";

    /// <summary>When non-empty, used as the public origin for QR codes and external links (no trailing slash). When empty, IAppUrlService uses the current HTTP request; Development may post-configure a LAN IPv4.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
