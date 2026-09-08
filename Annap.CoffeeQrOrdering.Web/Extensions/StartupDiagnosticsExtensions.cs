using Annap.CoffeeQrOrdering.Web.Services;
using Annap.CoffeeQrOrdering.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class StartupDiagnosticsExtensions
{
    public static void RegisterApplicationLifetimeDiagnostics(this WebApplication app)
    {
        ILogger? logger = null;
        try
        {
            logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Lifetime");
        }
        catch
        {
            // LoggerFactory may already be disposing during abnormal teardown.
        }

        if (app.Environment.IsDevelopment())
            DevelopmentShutdownWatchdog.Register(app);

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine($"ANNAP shutting down gracefully... PID={Environment.ProcessId}");
                var pending = DevelopmentBackgroundTaskTracker.PendingTaskNames();
                if (pending.Count > 0)
                    Console.WriteLine($"Waiting for background tasks: {string.Join(", ", pending)}");

                DevelopmentBackgroundTaskTracker.WaitForCompletion(TimeSpan.FromSeconds(8));
                Console.WriteLine("Stopping hosted services and releasing Kestrel...");
                logger?.LogInformation(
                    "ANNAP shutting down gracefully. PID={Pid}; pendingTasks={Pending}",
                    Environment.ProcessId,
                    pending.Count == 0 ? "(none)" : string.Join(", ", pending));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shutdown diagnostics error (ignored): {ex.Message}");
            }
        });

        app.Lifetime.ApplicationStopped.Register(() =>
        {
            try
            {
                Console.WriteLine("Hosted services stopped. Kestrel released.");
                Console.WriteLine($"ANNAP shutdown complete. PID={Environment.ProcessId}");
                Console.WriteLine();
                logger?.LogInformation(
                    "ANNAP shutdown complete. PID={Pid}",
                    Environment.ProcessId);
            }
            catch
            {
                // Never let logger disposal cascade into a failing exit code.
            }
        });
    }

    public static void VerifyMediaDirectories(this WebApplication app)
    {
        var env = app.Services.GetRequiredService<IWebHostEnvironment>();
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Media");
        var cloudinary = app.Configuration
            .GetSection(CloudinaryOptions.SectionName)
            .Get<CloudinaryOptions>() ?? new CloudinaryOptions();

        if (cloudinary.IsConfigured)
        {
            log.LogInformation(
                "Menu media storage: Cloudinary cloud={CloudName}; folder={Folder}",
                cloudinary.CloudName,
                cloudinary.Folder);
            return;
        }

        var managed = MenuImagePaths.ManagedDirectory(env);
        var originals = MenuImagePaths.OriginalsDirectory(env);

        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(originals);

        var managedExists = Directory.Exists(managed);
        var originalsExists = Directory.Exists(originals);
        var managedFiles = managedExists
            ? Directory.EnumerateFiles(managed, "*", SearchOption.TopDirectoryOnly).Count()
            : 0;

        log.LogInformation(
            "Menu media directories: managed={ManagedPath} exists={ManagedExists} files={ManagedFiles}; originals={OriginalsPath} exists={OriginalsExists}",
            managed,
            managedExists,
            managedFiles,
            originals,
            originalsExists);

        if (!managedExists || !originalsExists)
        {
            log.LogWarning(
                "One or more menu media directories could not be verified under WebRoot {WebRoot}",
                env.WebRootPath);
        }
    }

    public static void LogProductionStartupSummary(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            return;

        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Summary");
        var config = app.Services.GetRequiredService<IConfiguration>();
        var dbTarget = DatabaseStartupHelper.FormatConnectionTarget(config);
        var dbDiag = DatabaseStartupDiagnostics.SummaryForBanner();
        var applyMigrations = config.GetValue("Database:ApplyMigrationsOnStartup", false);
        var listening = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "(ASPNETCORE_URLS)";
        var renderDetected = InfrastructureEnvironment.IsRenderDeployment;
        var databaseSource = InfrastructureEnvironment.DatabaseConnectionSource;
        var qrReport = QrPublicUrlStartupDiagnostics.Build(app);
        var qr = qrReport.Resolution;

        if (qr.Warnings.Count > 0)
        {
            foreach (var warning in qr.Warnings)
                log.LogWarning("QR public URL: {Warning}", warning);
        }
        else if (string.IsNullOrWhiteSpace(qr.ConfiguredPublicBaseUrl)
                 && string.IsNullOrWhiteSpace(qr.DatabaseOverride)
                 && qr.Source == AppUrlResolutionSource.RequestHost)
        {
            log.LogInformation(
                "QR public URL uses request-host fallback (override and AppUrl__PublicBaseUrl are empty). Sample: {SampleQrUrl}",
                qrReport.EffectiveSampleQrUrl);
        }

        log.LogInformation(
            "Production startup summary: environment={Environment}; render={Render}; qrSource={QrSource}; qrHostname={QrHostname}; qrSample={QrSample}; databaseSource={DatabaseSource}; listening={Listening}; database target={DbTarget}; applyMigrationsOnStartup={ApplyMigrations}; bootstrap={Bootstrap}",
            app.Environment.EnvironmentName,
            renderDetected,
            qr.SourceLabel,
            qr.ResolvedHostname,
            qrReport.EffectiveSampleQrUrl,
            databaseSource,
            listening,
            dbTarget,
            applyMigrations,
            dbDiag);

        Console.WriteLine();
        Console.WriteLine("ANNAP PRODUCTION STARTUP");
        Console.WriteLine($"  Environment:       {app.Environment.EnvironmentName}");
        Console.WriteLine($"  Render deployment: {(renderDetected ? "yes" : "no")}");
        QrPublicUrlStartupDiagnostics.PrintToConsole(qrReport);
        Console.WriteLine($"  Database source:   {databaseSource}");
        Console.WriteLine($"  Listening:         {listening}");
        Console.WriteLine($"  Database:          {dbTarget}");
        Console.WriteLine($"  Bootstrap:         {dbDiag}");
        Console.WriteLine($"  Migrations on startup: {(applyMigrations ? "enabled" : "disabled")}");
        Console.WriteLine();
    }
}
