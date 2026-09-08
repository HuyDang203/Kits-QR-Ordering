namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Optional local-only URL hint for startup console banner (this machine). Public URLs use <see cref="AppUrlOptions"/>.</summary>
public sealed class LanDemoOptions
{
    public const string SectionName = "LanDemo";

    /// <summary>Developer machine URL for console hints (no trailing slash). Empty = derive from listen URLs.</summary>
    public string LocalBaseUrl { get; set; } = "";
}
