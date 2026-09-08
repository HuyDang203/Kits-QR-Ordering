using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class AnnapBootstrapExtensions
{
    public static int FindFirstFreeDevPort()
    {
        // Probe with IPAddress.Any (0.0.0.0) + ExclusiveAddressUse so the probe
        // matches exactly what Kestrel will attempt, preventing false-free readings.
        for (var port = 8080; port <= 8089; port++)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                listener.ExclusiveAddressUse = true;
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // port in use, try next
            }
        }
        return 8080; // all ports busy — Kestrel will surface the real error
    }

    public static CancellationTokenSource CreateRequestTimeout(HttpContext http, int timeoutSeconds)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }

    public static void StartDatabaseBootstrapDeferred(WebApplication app)
    {
        DevelopmentBackgroundTaskTracker.Start(
            "DbBootstrap",
            app.Lifetime,
            ct => RunDatabaseBootstrapDeferredAsync(app, ct));
    }

    public static async Task RunDatabaseBootstrapDeferredAsync(WebApplication app, CancellationToken stoppingToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.DbBootstrap");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken == default ? app.Lifetime.ApplicationStopping : stoppingToken);
        cts.CancelAfter(TimeSpan.FromMinutes(4));
        try
        {
            logger.LogInformation("Development DB bootstrap starting (deferred after ApplicationStarted).");
            await ApplyMigrationsAndSeedIfEnabledAsync(app, cts.Token).ConfigureAwait(false);
            await TryEnsureHospitalityCatalogBootstrapAsync(app, cts.Token).ConfigureAwait(false);
            await TryEnsureVenueTablesAsync(app, cts.Token).ConfigureAwait(false);
            logger.LogInformation("Development DB bootstrap completed.");
            await PaymentWorkflowSchemaStartupExtensions.ValidatePaymentWorkflowSchemaAsync(app, cts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            logger.LogWarning("Development DB bootstrap cancelled or timed out. Kestrel continues running.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Development DB bootstrap failed. Kestrel continues running. Message: {Message}. Open /admin/system/infrastructure after login for connection details.",
                ex.Message);
        }
    }

    public static async Task TryEnsureVenueTablesAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return;
            await DbInitializer.EnsureVenueTablesAsync(db, cancellationToken);
        }
        catch
        {
            // Schema not ready yet.
        }
    }

    public static async Task TryEnsureHospitalityCatalogBootstrapAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.MenuCatalog");
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return;
            await KitsMenuBootstrap.EnsureKitsMenuAsync(db, log, cancellationToken);
            await DbInitializer.TryArchiveLegacyDemoMenuIfReferencedAsync(db, cancellationToken);
            await DbInitializer.TryRemoveLegacyDemoMenuIfUnusedAsync(db, cancellationToken);
            await DbInitializer.TryRemoveLegacyCategoriesIfEmptyAsync(db, cancellationToken);
            var reconciled = await DbInitializer.TryReconcileKitsCatalogAsync(db, log, cancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(
                ex,
                "Kits menu bootstrap failed. Catalog may be incomplete.");
        }
    }

    public static async Task ApplyMigrationsAndSeedIfEnabledAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.DbBootstrap");

        if (!config.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            if (env.IsDevelopment())
                logger.LogInformation("Development DB bootstrap skipped: Database:ApplyMigrationsOnStartup is false.");
            else
                logger.LogInformation("Database migrate/seed skipped: Database:ApplyMigrationsOnStartup is false.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            if (!await DatabaseStartupHelper.WaitForPostgresAsync(db, config, logger, env, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "PostgreSQL was not reachable after the configured startup wait. Start Postgres (for example: docker compose up -d) and restart the web app.");
            }

            logger.LogInformation("Applying EF Core migrations for AppDbContext...");
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("EF Core migrations applied successfully.");
            DatabaseStartupDiagnostics.MarkMigrationsApplied(true);

            var menuLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.MenuCatalog");
            await KitsMenuBootstrap.EnsureKitsMenuAsync(db, menuLog, cancellationToken);
            var legacyArchived = await DbInitializer.TryArchiveLegacyDemoMenuIfReferencedAsync(db, cancellationToken);
            if (legacyArchived > 0)
                logger.LogInformation("Archived {Count} legacy Annap menu item(s) still referenced by historical orders.", legacyArchived);
            await DbInitializer.TryRemoveLegacyDemoMenuIfUnusedAsync(db, cancellationToken);
            await DbInitializer.TryRemoveLegacyCategoriesIfEmptyAsync(db, cancellationToken);
            var reconciled = await DbInitializer.TryReconcileKitsCatalogAsync(db, menuLog, cancellationToken);
            await DbInitializer.EnsureVenueTablesAsync(db, cancellationToken);

            var mediaMaintenance = scope.ServiceProvider.GetRequiredService<MenuMediaMaintenanceService>();
            await mediaMaintenance.RunAsync(cancellationToken: cancellationToken);

            logger.LogInformation(
                "Database bootstrap: kits menu seed (if empty), venue tables, menu media maintenance.");
        }
        catch (Exception ex)
        {
            DatabaseStartupDiagnostics.MarkMigrationsApplied(false);
            DatabaseStartupHelper.LogMigrationException(logger, config, ex);
            logger.LogError(
                "Recovery: verify PostgreSQL is reachable, then run migrations from the repository root if needed. Command: dotnet ef database update --project Annap.CoffeeQrOrdering.Infrastructure --startup-project Annap.CoffeeQrOrdering.Web");
            throw;
        }
    }
}
