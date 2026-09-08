using System.Diagnostics;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web;

internal static class DatabaseStartupHelper
{
    public sealed record DatabaseTarget(
        string Host,
        int Port,
        string Database,
        string Username,
        string Mode,
        string ComposeMode,
        string Display);

    /// <summary>Parses host:port/database for logs (no password).</summary>
    public static string FormatConnectionTarget(IConfiguration configuration)
        => ResolveConnectionTarget(configuration).Display;

    public static DatabaseTarget ResolveConnectionTarget(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new DatabaseTarget(
                "(missing)",
                0,
                "(missing)",
                "(missing)",
                "Configuration missing",
                ComposeMode(configuration),
                "(DefaultConnection missing)");
        }

        try
        {
            var csb = new NpgsqlConnectionStringBuilder(raw);
            var host = string.IsNullOrWhiteSpace(csb.Host) ? "localhost" : csb.Host.Trim();
            var port = csb.Port <= 0 ? 5432 : csb.Port;
            var database = string.IsNullOrWhiteSpace(csb.Database) ? "(default)" : csb.Database.Trim();
            var username = string.IsNullOrWhiteSpace(csb.Username) ? "(default)" : csb.Username.Trim();
            var mode = ResolveMode(host);
            return new DatabaseTarget(
                host,
                port,
                database,
                username,
                mode,
                ComposeMode(configuration),
                $"{host}:{port}/{database}");
        }
        catch
        {
            return new DatabaseTarget(
                "(invalid)",
                0,
                "(invalid)",
                "(invalid)",
                "Invalid connection string",
                ComposeMode(configuration),
                "(invalid connection string)");
        }
    }

    /// <summary>
    /// Waits until <see cref="DatabaseFacade.CanConnectAsync"/> succeeds or attempts exhausted.
    /// Handles "connection refused" while Docker Postgres is still starting.
    /// </summary>
    public static async Task<bool> WaitForPostgresAsync(
        AppDbContext db,
        IConfiguration configuration,
        ILogger logger,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var maxAttempts = configuration.GetValue("Database:Startup:MaxAttempts", 18);
        var initialMs = configuration.GetValue("Database:Startup:InitialDelayMilliseconds", 350);
        var maxDelayMs = configuration.GetValue("Database:Startup:MaxDelayMilliseconds", 10_000);
        var target = ResolveConnectionTarget(configuration);
        DatabaseStartupDiagnostics.SetConnectionDisplay(target.Display);

        logger.LogInformation(
            "Database target: Host={Host}; Port={Port}; Database={Database}; Mode={Mode}; Compose={ComposeMode}.",
            target.Host,
            target.Port,
            target.Database,
            target.Mode,
            target.ComposeMode);

        if (target.Port > 0)
        {
            var socketOk = await CanOpenTcpSocketAsync(target.Host, target.Port, TimeSpan.FromMilliseconds(900), cancellationToken)
                .ConfigureAwait(false);
            if (!socketOk)
            {
                logger.LogWarning(
                    "PostgreSQL socket is not reachable yet at {Host}:{Port}. {Hint}",
                    target.Host,
                    target.Port,
                    PortHint(target));
            }
        }

        logger.LogInformation(
            "Startup DB: waiting for PostgreSQL at {Target} (max {Attempts} attempts, quiet retry, backoff up to {MaxDelayMs}ms).",
            target.Display,
            maxAttempts,
            maxDelayMs);

        var sw = Stopwatch.StartNew();
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var ok = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
                if (ok)
                {
                    sw.Stop();
                    DatabaseStartupDiagnostics.MarkWaitFinished(true, sw.ElapsedMilliseconds, null);
                    if (attempt > 1)
                    {
                        logger.LogInformation(
                            "PostgreSQL reachable after attempt {Attempt}/{Max} (~{Ms}ms).",
                            attempt,
                            maxAttempts,
                            sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogInformation("PostgreSQL reachable on first attempt (~{Ms}ms).", sw.ElapsedMilliseconds);
                    }

                    return true;
                }

                if (attempt == 1 || attempt == maxAttempts)
                {
                    logger.LogWarning(
                        "PostgreSQL is not ready yet (attempt {Attempt}/{Max}).",
                        attempt,
                        maxAttempts);
                }
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                if (attempt == 1 || attempt % 4 == 0)
                {
                    if (configuration.GetValue("Diagnostics:VerboseSqlLogging", false))
                    {
                        logger.LogWarning(
                            ex,
                            "PostgreSQL connect attempt {Attempt}/{Max} failed. Retrying quietly.",
                            attempt,
                            maxAttempts);
                    }
                    else
                    {
                        logger.LogWarning(
                            "PostgreSQL connect attempt {Attempt}/{Max} failed: {Message}. Retrying quietly.",
                            attempt,
                            maxAttempts,
                            Condense(ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                if (configuration.GetValue("Diagnostics:VerboseSqlLogging", false))
                {
                    logger.LogError(
                        ex,
                        "PostgreSQL connect attempt {Attempt}/{Max} failed with no retries left.",
                        attempt,
                        maxAttempts);
                }
                else
                {
                    logger.LogError(
                        "PostgreSQL connect attempt {Attempt}/{Max} failed with no retries left: {Message}.",
                        attempt,
                        maxAttempts,
                        Condense(ex.Message));
                }
                sw.Stop();
                DatabaseStartupDiagnostics.MarkWaitFinished(false, sw.ElapsedMilliseconds, null);
                LogDockerHint(logger, environment, target);
                return false;
            }

            if (attempt >= maxAttempts)
                break;

            var backoff = Math.Min(maxDelayMs, (int)(initialMs * Math.Pow(1.75, attempt - 1)));
            await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
        }

        sw.Stop();
        logger.LogError(
            "PostgreSQL not reachable after {Max} attempts (~{Ms}ms). Expected: {Target}.",
            maxAttempts,
            sw.ElapsedMilliseconds,
            target.Display);
        DatabaseStartupDiagnostics.MarkWaitFinished(false, sw.ElapsedMilliseconds, null);
        LogDockerHint(logger, environment, target);
        return false;
    }

    private static void LogDockerHint(ILogger logger, IHostEnvironment environment, DatabaseTarget target)
    {
        logger.LogError(
            """
            PostgreSQL is unavailable.

            Database target:
              Host={Host}
              Port={Port}
              Database={Database}
              Mode={Mode}

            Verify:
              - Docker Desktop is running
              - postgres container is healthy
              - port 5432 is exposed only by annap-postgres

            Suggested commands:
              docker ps
              docker logs annap-postgres
              docker compose up -d
            """,
            target.Host,
            target.Port,
            target.Database,
            target.Mode);
        if (environment.IsDevelopment())
        {
            logger.LogInformation(
                "Development mode: the web server keeps running; pages that need the database will error until Postgres is up.");
        }
    }

    public static void LogMigrationException(ILogger logger, IConfiguration configuration, Exception ex)
    {
        var verbose = configuration.GetValue("Diagnostics:VerboseSqlLogging", false);
        if (verbose)
            logger.LogError(ex, "Database migration or seed failed.");
        else
            logger.LogError(
                "Database migration or seed failed: {Message}. Enable Diagnostics:VerboseSqlLogging for full details.",
                ex.Message);
    }

    private static async Task<bool> CanOpenTcpSocketAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
            return false;

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            if (completed != connectTask)
                return false;

            await connectTask.ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string ComposeMode(IConfiguration configuration)
        => Environment.GetEnvironmentVariable("ANNAP_COMPOSE_MODE")
           ?? configuration["ANNAP_COMPOSE_MODE"]
           ?? "host-dev";

    private static string ResolveMode(string host)
    {
        if (host.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            return "Container network -> postgres:5432";
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return "Host machine -> Docker PostgreSQL";
        if (host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase))
            return "Container -> host machine";
        return "External PostgreSQL host";
    }

    private static string PortHint(DatabaseTarget target)
    {
        if (target.Host.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            return "Inside Docker, the web container must share a compose network with service name 'postgres'.";
        if (target.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || target.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return "From the host machine, start the DB with 'docker compose up -d' and confirm 5432:5432 is on annap-postgres.";
        return "Confirm the configured DB host and firewall allow TCP access.";
    }

    private static string Condense(string? message)
    {
        var text = (message ?? "").Replace(Environment.NewLine, " ").Trim();
        return text.Length <= 220 ? text : text[..217] + "...";
    }
}
