using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Web.Services;

internal static class QrPublicUrlStartupDiagnostics
{
    public static QrPublicUrlStartupReport Build(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var appUrl = scope.ServiceProvider.GetRequiredService<IAppUrlService>();
        var resolution = appUrl.DescribeResolution(null);
        var renderExternal = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL")?.Trim().TrimEnd('/');

        return new QrPublicUrlStartupReport(
            resolution,
            string.IsNullOrEmpty(renderExternal) ? null : renderExternal,
            InfrastructureEnvironment.IsRenderDeployment);
    }

    public static void PrintToConsole(QrPublicUrlStartupReport report)
    {
        var r = report.Resolution;
        Console.WriteLine("  QR public URL source:");
        Console.WriteLine($"    Active source:     {r.SourceLabel}");
        Console.WriteLine($"    Resolved hostname: {r.ResolvedHostname}");
        Console.WriteLine($"    Resolved base URL: {(string.IsNullOrWhiteSpace(r.ResolvedBaseUrl) ? "(request host at QR generation)" : r.ResolvedBaseUrl)}");
        Console.WriteLine($"    Sample QR (T01):   {report.EffectiveSampleQrUrl}");

        var db = string.IsNullOrWhiteSpace(r.DatabaseOverride) ? "(empty)" : r.DatabaseOverride;
        var cfg = string.IsNullOrWhiteSpace(r.ConfiguredPublicBaseUrl) ? "(empty)" : r.ConfiguredPublicBaseUrl;
        Console.WriteLine($"    DB override:       {db}");
        Console.WriteLine($"    AppUrl config:     {cfg}");

        if (!string.IsNullOrWhiteSpace(report.RenderExternalUrl))
            Console.WriteLine($"    Render service:    {report.RenderExternalUrl}");

        foreach (var warning in r.Warnings)
            Console.WriteLine($"    WARNING: {warning}");
    }
}
