namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Startup snapshot of QR public URL configuration (no HTTP request required).</summary>
public sealed record QrPublicUrlStartupReport(
    AppUrlResolution Resolution,
    string? RenderExternalUrl,
    bool IsRenderDeployment)
{
    public string EffectiveSampleQrUrl => Resolution.SampleTableQrUrl("T01");
}
