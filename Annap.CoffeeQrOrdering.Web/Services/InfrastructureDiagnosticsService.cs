using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class InfrastructureDiagnosticsService(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    AppDbContext db)
{
    public sealed record InfrastructureReport(
        string EnvironmentName,
        string ComposeMode,
        string PublicBaseUrl,
        string DatabaseHost,
        int DatabasePort,
        string DatabaseName,
        string DatabaseUser,
        string DatabaseMode,
        bool SocketReachable,
        bool EfReachable,
        string ExpectedPostgresContainer,
        string ExpectedWebContainer,
        string PostgresPortMapping,
        string WebPortMapping,
        string DockerVisibility,
        string PortHint,
        IReadOnlyList<string> RecoveryCommands);

    public async Task<InfrastructureReport> BuildAsync(string requestBaseUrl, CancellationToken cancellationToken)
    {
        var target = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
        var socket = await CanOpenTcpSocketAsync(target.Host, target.Port, TimeSpan.FromMilliseconds(900), cancellationToken)
            .ConfigureAwait(false);

        var efReachable = false;
        try
        {
            efReachable = await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            efReachable = false;
        }

        return new InfrastructureReport(
            environment.EnvironmentName,
            target.ComposeMode,
            string.IsNullOrWhiteSpace(requestBaseUrl) ? "(unknown)" : requestBaseUrl,
            target.Host,
            target.Port,
            target.Database,
            target.Username,
            target.Mode,
            socket,
            efReachable,
            "annap-postgres",
            "annap-web",
            "5432:5432",
            "8080:8080",
            "Docker daemon state is checked from the host with docker ps/logs; the app does not require Docker socket access.",
            ResolvePortHint(target.Host),
            [
                "docker ps",
                "docker logs annap-postgres",
                "docker compose up -d"
            ]);
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

    private static string ResolvePortHint(string host)
    {
        if (host.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            return "Container mode: web should connect to postgres:5432 on the compose network. Do not map 5432 on annap-web.";
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return "Host mode: dotnet run should connect to localhost:5432, exposed by annap-postgres.";
        return "External mode: confirm DNS, firewall, and PostgreSQL listen addresses.";
    }
}
