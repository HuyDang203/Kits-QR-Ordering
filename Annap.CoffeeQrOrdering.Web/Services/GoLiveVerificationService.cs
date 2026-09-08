using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Automated production go-live gates. Operational verification only.</summary>
public sealed class GoLiveVerificationService(
    AppDbContext db,
    IMenuInventoryGate inventoryGate,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    HealthCheckService healthCheckService,
    IHttpClientFactory httpClientFactory,
    IAppUrlService appUrlService)
{
    public async Task<int> RunAsync(WebApplication app, CancellationToken cancellationToken)
    {
        var dbTarget = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
        Console.WriteLine("Kits Go-Live Verification");
        Console.WriteLine("==========================");
        Console.WriteLine();

        var databaseGate = await SafeGateAsync(
            () => VerifyDatabaseAsync(dbTarget, cancellationToken),
            "Database").ConfigureAwait(false);
        PrintDatabaseSection(databaseGate, dbTarget, environment.EnvironmentName);

        var dbReady = databaseGate.Passed;

        var menuCatalogGate = dbReady
            ? await SafeGateAsync(() => VerifyKitsMenuCatalogAsync(cancellationToken), "Kits Menu Catalog").ConfigureAwait(false)
            : Skipped("Kits Menu Catalog", "Database unreachable.");

        var signatureGate = dbReady
            ? await SafeGateAsync(() => VerifySignatureIntegrityAsync(cancellationToken), "Signature Integrity").ConfigureAwait(false)
            : Skipped("Signature Integrity", "Database unreachable.");

        var healthGate = await SafeGateAsync(
            () => VerifyHealthAsync(app, dbReady, cancellationToken),
            "Health").ConfigureAwait(false);
        PrintHealthSection(healthGate);

        var gates = new[]
        {
            databaseGate,
            menuCatalogGate,
            signatureGate,
            healthGate
        };

        var ready = gates.All(g => g.Passed);
        var warnings = new List<string>();
        var renderAppUrlWarning = BuildQrPublicUrlWarnings();
        if (renderAppUrlWarning.Count > 0)
            warnings.AddRange(renderAppUrlWarning);
        PrintSummary(gates, ready, warnings);

        return ready ? 0 : 1;
    }

    private async Task<GoLiveGateResult> VerifyDatabaseAsync(
        DatabaseStartupHelper.DatabaseTarget dbTarget,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
        {
            return Fail(
                "Database",
                "ConnectionStrings:DefaultConnection is missing.");
        }

        try
        {
            var ok = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                return Fail(
                    "Database",
                    $"PostgreSQL not reachable at {dbTarget.Display}.");
            }

            return Pass("Database");
        }
        catch (Exception ex)
        {
            return Fail(
                "Database",
                $"PostgreSQL connection failed at {dbTarget.Display}: {Condense(ex.Message)}");
        }
    }

    /// <summary>
    /// Verifies the four required Kits categories are present with their canonical sort order
    /// and that each catalog-keyed category has at least one item. Does NOT enforce a fixed
    /// item count — admin may archive items.
    /// </summary>
    private async Task<GoLiveGateResult> VerifyKitsMenuCatalogAsync(CancellationToken cancellationToken)
    {
        var requiredCategories = new[]
        {
            KitsMenuCatalog.Categories.Cafe,
            KitsMenuCatalog.Categories.Tra,
            KitsMenuCatalog.Categories.Banh,
            KitsMenuCatalog.Categories.Combo,
        };
        var expectedSort = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [KitsMenuCatalog.Categories.Cafe] = KitsMenuCatalog.SortCafe,
            [KitsMenuCatalog.Categories.Tra] = KitsMenuCatalog.SortTra,
            [KitsMenuCatalog.Categories.Banh] = KitsMenuCatalog.SortBanh,
            [KitsMenuCatalog.Categories.Combo] = KitsMenuCatalog.SortCombo,
        };

        var categories = await db.MenuCategories
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var required in requiredCategories)
        {
            var cat = categories.FirstOrDefault(c =>
                string.Equals(c.Name, required, StringComparison.OrdinalIgnoreCase));
            if (cat is null)
            {
                return Fail(
                    "Kits Menu Catalog",
                    $"Required category '{required}' is missing.");
            }

            if (cat.SortOrder != expectedSort[required])
            {
                return Fail(
                    "Kits Menu Catalog",
                    $"Category '{required}' has SortOrder={cat.SortOrder}, expected {expectedSort[required]}.");
            }
        }

        var kitsKeyPrefix = KitsMenuCatalog.CatalogKeyPrefix;
        var kitsItemCount = await db.MenuItems
            .AsNoTracking()
            .CountAsync(m => m.CatalogKey != null && m.CatalogKey.StartsWith(kitsKeyPrefix), cancellationToken)
            .ConfigureAwait(false);

        if (kitsItemCount == 0)
        {
            return Fail(
                "Kits Menu Catalog",
                $"No menu items found with catalog key prefix '{kitsKeyPrefix}'.");
        }

        return Pass("Kits Menu Catalog");
    }

    /// <summary>
    /// Flags any <c>IsSignature=true</c> item in non-Kits categories (e.g. legacy "Specialty Coffee"
    /// rows from old data) so they can be cleaned up before go-live. We do not modify data — we
    /// only report.
    /// </summary>
    private async Task<GoLiveGateResult> VerifySignatureIntegrityAsync(CancellationToken cancellationToken)
    {
        var allowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KitsMenuCatalog.Categories.Cafe,
            KitsMenuCatalog.Categories.Tra,
            KitsMenuCatalog.Categories.Banh,
            KitsMenuCatalog.Categories.Combo,
        };

        var unexpected = await db.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m =>
                m.IsSignature
                && m.Category != null
                && !allowedCategories.Contains(m.Category.Name))
            .Select(m => new { m.CatalogKey, m.Name, Category = m.Category!.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unexpected.Count == 0)
            return Pass("Signature Integrity");

        var sample = string.Join(
            ", ",
            unexpected.Take(6).Select(m =>
                $"{m.Name} ({m.Category}, key={m.CatalogKey ?? "null"})"));

        return Fail(
            "Signature Integrity",
            $"{unexpected.Count} unexpected IsSignature=true item(s) outside Kits categories: {sample}.");
    }

    private async Task<GoLiveGateResult> VerifyHealthAsync(
        WebApplication app,
        bool databaseReady,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        try
        {
            var healthReport = await healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            if (healthReport.Status != HealthStatus.Healthy)
            {
                var unhealthy = healthReport.Entries
                    .Where(e => e.Value.Status != HealthStatus.Healthy)
                    .Select(e => $"{e.Key}={e.Value.Status}")
                    .ToList();
                failures.Add(
                    unhealthy.Count > 0
                        ? $"Health checks unhealthy: {string.Join(", ", unhealthy)}."
                        : $"Health checks status={healthReport.Status}.");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Health check service failed: {Condense(ex.Message)}.");
        }

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!canConnect)
                failures.Add("Database connectivity check failed inside health verification.");
        }
        catch (Exception ex)
        {
            failures.Add($"Database connectivity check failed: {Condense(ex.Message)}.");
        }

        var endpointFailure = databaseReady
            ? await VerifyHealthEndpointAsync(app, cancellationToken).ConfigureAwait(false)
            : "Skipped /health probe because database is unreachable.";
        if (endpointFailure is not null)
            failures.Add(endpointFailure);

        if (failures.Count == 0)
            return Pass("Health");

        return Fail("Health", string.Join(" ", failures));
    }

    private async Task<string?> VerifyHealthEndpointAsync(
        WebApplication app,
        CancellationToken cancellationToken)
    {
        var configuredUrl = ResolveConfiguredHealthUrl();
        var startedLocally = false;

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (app.Urls.Count == 0)
                app.Urls.Add("http://127.0.0.1:0");

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            startedLocally = true;
            configuredUrl = ResolveListeningHealthUrl(app);
        }

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            if (startedLocally)
                await app.StopAsync(cancellationToken).ConfigureAwait(false);

            return "Application health endpoint URL could not be resolved.";
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(GoLiveVerificationService));
            client.Timeout = TimeSpan.FromSeconds(12);
            using var response = await client.GetAsync(configuredUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return $"GET {configuredUrl} returned HTTP {(int)response.StatusCode}.";
            }

            var body = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
            if (body.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
                return $"GET {configuredUrl} reported Unhealthy.";

            if (!body.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
                return $"GET {configuredUrl} did not report Healthy (body='{Condense(body)}').";
        }
        catch (Exception ex)
        {
            return $"GET {configuredUrl} failed: {Condense(ex.Message)}.";
        }
        finally
        {
            if (startedLocally)
                await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private string? ResolveConfiguredHealthUrl()
    {
        var explicitUrl = configuration["GoLive:HealthUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitUrl))
            return NormalizeHealthUrl(explicitUrl);

        var publicBase = configuration["AppUrl:PublicBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(publicBase))
            return NormalizeHealthUrl(publicBase);

        return null;
    }

    private static string? ResolveListeningHealthUrl(WebApplication app)
    {
        var address = app.Urls.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(address))
            return null;

        if (address.Contains(":0", StringComparison.Ordinal))
        {
            var server = app.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var feature = server?.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            address = feature?.Addresses.FirstOrDefault();
        }

        return string.IsNullOrWhiteSpace(address) ? null : NormalizeHealthUrl(address);
    }

    private static string NormalizeHealthUrl(string baseOrUrl)
    {
        var trimmed = baseOrUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/health", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/health";
    }

    private IReadOnlyList<string> BuildQrPublicUrlWarnings()
    {
        var messages = new List<string>();
        var resolution = appUrlService.DescribeResolution(null);

        foreach (var warning in resolution.Warnings)
            messages.Add($"QR public URL: WARN — {warning}");

        if (InfrastructureEnvironment.IsRenderDeployment
            && string.IsNullOrWhiteSpace(resolution.ConfiguredPublicBaseUrl)
            && string.IsNullOrWhiteSpace(resolution.DatabaseOverride)
            && resolution.Source == AppUrlResolutionSource.RequestHost)
        {
            messages.Add(
                "QR public URL: INFO — Using request-host fallback (override and AppUrl__PublicBaseUrl empty). "
                + $"Sample: {resolution.SampleTableQrUrl("T01")}. "
                + "Set AppUrl__PublicBaseUrl=https://<your-service>.onrender.com for explicit QR hostname.");
        }

        return messages;
    }

    private static void PrintHealthSection(GoLiveGateResult healthGate)
    {
        Console.WriteLine("4. Health verification");
        Console.WriteLine($"   Application health: {(healthGate.Passed ? "PASS" : "FAIL")}");
        if (!healthGate.Passed && healthGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {healthGate.FailureReason}");
        Console.WriteLine();
    }

    private static void PrintDatabaseSection(
        GoLiveGateResult databaseGate,
        DatabaseStartupHelper.DatabaseTarget dbTarget,
        string environmentName)
    {
        Console.WriteLine("1. Database connectivity");
        Console.WriteLine($"   Database: {dbTarget.Database}");
        Console.WriteLine($"   Environment: {environmentName}");
        Console.WriteLine($"   Target: {dbTarget.Display}");
        Console.WriteLine($"   Status: {(databaseGate.Passed ? "REACHABLE" : "UNREACHABLE")}");
        if (!databaseGate.Passed && databaseGate.FailureReason is not null)
            Console.WriteLine($"   Reason: {databaseGate.FailureReason}");
        Console.WriteLine();
    }

    private static void PrintSummary(IReadOnlyList<GoLiveGateResult> gates, bool ready, IReadOnlyList<string> warnings)
    {
        Console.WriteLine("====================================");
        Console.WriteLine("KITS GO-LIVE REPORT");
        Console.WriteLine("===================");
        Console.WriteLine();

        foreach (var gate in gates)
        {
            Console.WriteLine($"{FormatGateLabel(gate.Name)} {(gate.Passed ? "PASS" : "FAIL")}");
            if (!gate.Passed && !string.IsNullOrWhiteSpace(gate.FailureReason))
                Console.WriteLine($"  -> {gate.FailureReason}");
        }

        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("WARNINGS (non-blocking):");
            foreach (var warning in warnings)
                Console.WriteLine($"  ! {warning}");
        }

        Console.WriteLine();
        Console.WriteLine("FINAL STATUS:");
        Console.WriteLine();
        if (ready)
        {
            Console.WriteLine("READY FOR PRODUCTION TRAFFIC");
        }
        else
        {
            Console.WriteLine("NOT READY FOR PRODUCTION TRAFFIC");
            var reasons = gates
                .Where(g => !g.Passed && !string.IsNullOrWhiteSpace(g.FailureReason))
                .Select(g => $"{g.Name}: {g.FailureReason}")
                .ToList();
            if (reasons.Count > 0)
                Console.WriteLine(string.Join(Environment.NewLine, reasons));
        }
    }

    private static string FormatGateLabel(string name)
    {
        const int width = 22;
        var dots = Math.Max(1, width - name.Length - 1);
        return $"{name} {new string('.', dots)}";
    }

    private static GoLiveGateResult Pass(string name) => new(name, true, null);

    private static GoLiveGateResult Fail(string name, string reason) => new(name, false, reason);

    private static string Condense(string? message)
    {
        var text = (message ?? "").Replace(Environment.NewLine, " ").Trim();
        return text.Length <= 220 ? text : text[..217] + "...";
    }

    private static async Task<GoLiveGateResult> SafeGateAsync(
        Func<Task<GoLiveGateResult>> verify,
        string gateName)
    {
        try
        {
            return await verify().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(gateName, Condense(ex.Message));
        }
    }

    private static GoLiveGateResult Skipped(string name, string reason) => Fail(name, reason);

    private sealed record GoLiveGateResult(string Name, bool Passed, string? FailureReason);
}