using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Extensions;
using Annap.CoffeeQrOrdering.Web.Services;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

InfrastructureEnvironment.LoadDotEnvIfPresent();
var builder = WebApplication.CreateBuilder(args);
InfrastructureEnvironment.ApplyPostgresEnvironmentConnection(builder);
var isVerifyGoLive = args.Any(a => string.Equals(a, "verify-go-live", StringComparison.OrdinalIgnoreCase));

var devPort = 0;
if (builder.Environment.IsDevelopment())
{
    DevelopmentHostDiagnostics.AssertNoConcurrentDevHosts(builder.Environment);
    DevelopmentHostDiagnostics.PrintStaleProcessHintsIfNeeded(builder.Environment);
    devPort = AnnapBootstrapExtensions.FindFirstFreeDevPort();
    if (devPort != 8080)
    {
        Console.WriteLine();
        Console.WriteLine($"  [dev] Preferred port 8080 is already in use.");
        Console.WriteLine($"        Application starting on http://localhost:{devPort}");
        Console.WriteLine();
        Console.WriteLine($"  To identify and free port 8080:");
        Console.WriteLine($"    netstat -ano | findstr :8080");
        Console.WriteLine($"    taskkill /PID <PID> /F");
        DevelopmentHostDiagnostics.PrintPortOwnerHint(8080);
        Console.WriteLine();
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{devPort}");
    builder.Services.AddCors(o =>
    {
        o.AddPolicy("DevelopmentLan", p =>
            p.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin))
                        return false;
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var u))
                        return false;
                    if (u.Scheme is not ("http" or "https"))
                        return false;
                    if (u.Port != devPort)
                        return false;
                    if (u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (u.Host == "127.0.0.1")
                        return true;
                    if (!IPAddress.TryParse(u.Host, out var ip))
                        return false;
                    if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        return false;
                    var b = ip.GetAddressBytes();
                    if (b[0] == 10)
                        return true;
                    if (b[0] == 192 && b[1] == 168)
                        return true;
                    if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                        return true;
                    return false;
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });
}

builder.AddAnnapWebServices(devPort);

var app = builder.Build();

ProductionStartupGuard.Validate(app.Environment, app.Configuration);
app.RegisterApplicationLifetimeDiagnostics();

app.UseAnnapMiddleware();
app.MapAnnapEndpoints();

if (isVerifyGoLive)
{
    await using var scope = app.Services.CreateAsyncScope();
    var exitCode = await scope.ServiceProvider
        .GetRequiredService<GoLiveVerificationService>()
        .RunAsync(app, CancellationToken.None)
        .ConfigureAwait(false);
    Environment.Exit(exitCode);
    return;
}

LanDemoStartupBanner.Register(app);

if (app.Environment.IsDevelopment())
    app.Lifetime.ApplicationStarted.Register(() => AnnapBootstrapExtensions.StartDatabaseBootstrapDeferred(app));
else
{
    var prodBootstrapLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.DbBootstrap");
    try
    {
        await AnnapBootstrapExtensions.ApplyMigrationsAndSeedIfEnabledAsync(app, CancellationToken.None);
        await AnnapBootstrapExtensions.TryEnsureHospitalityCatalogBootstrapAsync(app, CancellationToken.None);
        await AnnapBootstrapExtensions.TryEnsureVenueTablesAsync(app, CancellationToken.None);
        prodBootstrapLogger.LogInformation("Production database bootstrap finished.");
        await PaymentWorkflowSchemaStartupExtensions.ValidatePaymentWorkflowSchemaAsync(app, CancellationToken.None);
    }
    catch (Exception ex)
    {
        prodBootstrapLogger.LogCritical(ex, "Production database bootstrap failed; stopping startup.");
        throw;
    }
}

app.VerifyMediaDirectories();
app.LogProductionStartupSummary();

if (app.Environment.IsDevelopment())
    DevelopmentHostDiagnostics.PrintPreRunDiagnostics(app);

if (args.Contains("--migrate-images-only", StringComparer.OrdinalIgnoreCase))
{
    var migrateLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MenuImageMigration");
    var migrateEnv = app.Services.GetRequiredService<IWebHostEnvironment>();
    MenuImageMigrationService.MigrateManagedUploadsAsync(migrateEnv, null, migrateLog).GetAwaiter().GetResult();
    MenuImageMigrationService.MigrateStaticAssetsAsync(migrateEnv, migrateLog).GetAwaiter().GetResult();
    Console.WriteLine("[migrate-images] no-op: Kits menu does not seed from external assets.");
    return;
}

app.Run();
