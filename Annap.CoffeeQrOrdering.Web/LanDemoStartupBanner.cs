using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web;

internal static class LanDemoStartupBanner
{
    public static void Register(WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            DevelopmentBackgroundTaskTracker.Start("LanDemoBanner", app.Lifetime, async stoppingToken =>
            {
                try
                {
                    await using var scope = app.Services.CreateAsyncScope();
                    var hostEnv = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
                    var appUrl = scope.ServiceProvider.GetRequiredService<IOptions<AppUrlOptions>>().Value;
                    var lanDemo = scope.ServiceProvider.GetRequiredService<IOptions<LanDemoOptions>>().Value;
                    var listening = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "(ASPNETCORE_URLS)";
                    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AppUrl");
                    var detector = scope.ServiceProvider.GetService<ILanIpDetector>();

                    var dbReachable = false;
                    try
                    {
                        using var dbCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        dbCts.CancelAfter(TimeSpan.FromSeconds(8));
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        dbReachable = await db.Database.CanConnectAsync(dbCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Startup DB reachability check failed.");
                    }

                    var port = 8080;
                    if (Uri.TryCreate((appUrl.PublicBaseUrl ?? "").Trim(), UriKind.Absolute, out var pubUri) && pubUri.Port > 0)
                        port = pubUri.Port;

                    var detectedIp = detector?.TryGetPreferredLanIPv4();
                    var detectedWifi = !string.IsNullOrEmpty(detectedIp)
                        ? $"http://{detectedIp}:{port}"
                        : "";

                    var configured = (appUrl.PublicBaseUrl ?? "").TrimEnd('/');
                    var wifi = configured;

                    static bool HostsMatch(string a, string b)
                    {
                        if (!Uri.TryCreate(a, UriKind.Absolute, out var ua) || !Uri.TryCreate(b, UriKind.Absolute, out var ub))
                            return false;
                        return ua.Host.Equals(ub.Host, StringComparison.OrdinalIgnoreCase) && ua.Port == ub.Port;
                    }

                    var mismatch = !string.IsNullOrEmpty(detectedIp)
                        && !string.IsNullOrEmpty(configured)
                        && !HostsMatch(detectedWifi, configured);

                    var local = string.IsNullOrWhiteSpace(lanDemo.LocalBaseUrl)
                        ? $"http://localhost:{port}"
                        : lanDemo.LocalBaseUrl.TrimEnd('/');

                    var wifiLine = string.IsNullOrEmpty(wifi) ? "(auto / request — see /admin/system/network)" : wifi;

                    static bool ListensAllInterfaces(string urls) =>
                        urls.Contains("0.0.0.0:", StringComparison.OrdinalIgnoreCase)
                        || urls.Contains("http://*:", StringComparison.OrdinalIgnoreCase)
                        || urls.Contains("http://[::]:", StringComparison.OrdinalIgnoreCase);

                    var kestrelAllIfaces = ListensAllInterfaces(listening);

                    // Prefer configured public URL, else detected LAN, else local (same machine only).
                    var lanBase = !string.IsNullOrEmpty(wifi)
                        ? wifi
                        : !string.IsNullOrEmpty(detectedWifi)
                            ? detectedWifi
                            : local;

                    if (hostEnv.IsDevelopment())
                    {
                        PrintDevelopmentBanner(local, lanBase, listening, dbReachable, kestrelAllIfaces, detectedWifi, wifiLine, mismatch);
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Annap public base: {lanBase} · Database: {(dbReachable ? "reachable" : "not reachable")} · Listening: {listening}");
                        Console.WriteLine();
                    }

                    log.LogInformation(
                        "AppUrl: listening={Listening}; dbReachable={Db}; detectedWifi={Detected}; configured={Configured}; mismatch={Mismatch}; ping={Ping}",
                        listening,
                        dbReachable,
                        string.IsNullOrEmpty(detectedWifi) ? "(n/a)" : detectedWifi,
                        configured,
                        mismatch,
                        string.IsNullOrEmpty(wifi) ? "(n/a)" : wifi + "/api/diag/ping");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    /* shutdown during banner */
                }
                catch
                {
                    /* ignore banner failures */
                }
            });
        });
    }

    private static void PrintDevelopmentBanner(
        string local,
        string lanBase,
        string listening,
        bool dbReachable,
        bool kestrelAllIfaces,
        string detectedWifi,
        string wifiLine,
        bool mismatch)
    {
        static bool IsHttpBase(string s) => Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

        var linkBase = IsHttpBase(lanBase) ? lanBase.TrimEnd('/') : local.TrimEnd('/');
        var qrAdmin = linkBase + "/admin/demo/qr";
        var admin = linkBase + "/admin";
        var guestTableSample = linkBase + "/table/T01";

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("ANNAP DEVELOPMENT SERVER");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine("Local:");
        Console.WriteLine(local);
        Console.WriteLine();
        Console.WriteLine("LAN (phones / QR):");
        Console.WriteLine(IsHttpBase(lanBase) ? lanBase : $"{local}  ← set AppUrl:PublicBaseUrl or open /admin/system/network if LAN unknown");
        Console.WriteLine();
        Console.WriteLine("QR (admin print cards):");
        Console.WriteLine(qrAdmin);
        Console.WriteLine();
        Console.WriteLine("Guest scan sample (table T01):");
        Console.WriteLine(guestTableSample);
        Console.WriteLine();
        Console.WriteLine("Admin:");
        Console.WriteLine(admin);
        Console.WriteLine("=============================");
        Console.WriteLine($"Kestrel: {listening} · Database: {(dbReachable ? "yes" : "no")} · All interfaces: {(kestrelAllIfaces ? "yes" : "check URLs")}");
        if (!string.IsNullOrEmpty(detectedWifi) && IsHttpBase(detectedWifi))
        {
            Console.WriteLine($"Detected adapter: {detectedWifi} (compare with public base above)");
        }
        else if (string.IsNullOrEmpty(detectedWifi))
        {
            Console.WriteLine("LAN adapter: (not detected — use Local or set AppUrl / admin network)");
        }

        if (!IsHttpBase(wifiLine) && wifiLine.Contains("auto", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine($"Public URL hint: {wifiLine}");

        if (mismatch)
        {
            Console.WriteLine();
            Console.WriteLine("WARNING: AppUrl:PublicBaseUrl host does not match detected WiFi IP — phones may need the detected URL.");
        }

        Console.WriteLine("Diag: " + linkBase + "/diag/mobile · Ping: " + linkBase + "/api/diag/ping");
        Console.WriteLine();
    }
}
