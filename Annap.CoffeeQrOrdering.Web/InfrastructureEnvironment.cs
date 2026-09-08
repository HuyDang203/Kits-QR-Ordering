using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web;

internal static class InfrastructureEnvironment
{
    internal static bool IsRenderDeployment { get; private set; }

    internal static string DatabaseConnectionSource { get; private set; } = "ConnectionStrings:DefaultConnection (appsettings)";

    public static void LoadDotEnvIfPresent()
    {
        var path = Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path))
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"', '\'');
            if (key.Length == 0 || Environment.GetEnvironmentVariable(key) is not null)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static void ApplyPostgresEnvironmentConnection(WebApplicationBuilder builder)
    {
        IsRenderDeployment = DetectRenderDeployment();

        var explicitConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(explicitConn))
        {
            builder.Configuration["ConnectionStrings:DefaultConnection"] =
                NormalizeConnectionString(explicitConn, builder.Environment.IsProduction());
            DatabaseConnectionSource = "ConnectionStrings__DefaultConnection";
            return;
        }

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            try
            {
                var npgsql = ConvertDatabaseUrlToNpgsql(
                    databaseUrl,
                    builder.Environment.IsProduction());
                builder.Configuration["ConnectionStrings:DefaultConnection"] = npgsql;
                DatabaseConnectionSource = "DATABASE_URL";
                Console.WriteLine("Using DATABASE_URL-derived PostgreSQL connection.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: DATABASE_URL could not be parsed: {ex.Message}");
            }
        }

        var hasPostgresEnv =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_DB")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_USER")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));

        if (!hasPostgresEnv)
            return;

        var composeMode = Environment.GetEnvironmentVariable("ANNAP_COMPOSE_MODE") ?? "";
        var host = builder.Environment.IsProduction() || composeMode.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "postgres"
            : "localhost";

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = 5432,
            Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "annap_qr_ordering",
            Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres",
            Password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres",
            Timeout = 15,
            SslMode = builder.Environment.IsProduction()
                && !IsLocalOrComposePostgresHost(host)
                ? SslMode.Require
                : SslMode.Prefer
        };

        builder.Configuration["ConnectionStrings:DefaultConnection"] = csb.ConnectionString;
        DatabaseConnectionSource = "POSTGRES_*";
    }

    internal static string ConvertDatabaseUrlToNpgsql(string databaseUrl, bool enforceSsl = false)
    {
        var trimmed = databaseUrl.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("DATABASE_URL is empty.", nameof(databaseUrl));

        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("DATABASE_URL must use postgres:// or postgresql:// scheme.", nameof(databaseUrl));
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo;
        string username = "";
        string password = "";
        if (!string.IsNullOrEmpty(userInfo))
        {
            var colon = userInfo.IndexOf(':');
            if (colon >= 0)
            {
                username = Uri.UnescapeDataString(userInfo[..colon]);
                password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
            }
            else
            {
                username = Uri.UnescapeDataString(userInfo);
            }
        }

        var database = uri.AbsolutePath.TrimStart('/');
        if (string.IsNullOrEmpty(database))
            database = "postgres";

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
            Timeout = 15
        };

        ApplySslFromQuery(csb, uri.Query);

        EnforceSslIfRequired(csb, enforceSsl || IsRenderDeployment);

        return csb.ConnectionString;
    }

    internal static string NormalizeConnectionString(string connectionString, bool enforceSsl)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        EnforceSslIfRequired(csb, enforceSsl);
        return csb.ConnectionString;
    }

    private static void EnforceSslIfRequired(NpgsqlConnectionStringBuilder csb, bool enforceSsl)
    {
        if (!enforceSsl)
            return;

        // Docker Compose / local loopback Postgres is not TLS-terminated.
        if (IsLocalOrComposePostgresHost(csb.Host))
            return;

        if (csb.SslMode == SslMode.Disable)
        {
            throw new InvalidOperationException(
                "Production PostgreSQL connections cannot disable SSL.");
        }

        if (csb.SslMode == SslMode.Prefer)
            csb.SslMode = SslMode.Require;
    }

    private static bool IsLocalOrComposePostgresHost(string? host) =>
        !string.IsNullOrWhiteSpace(host)
        && (host.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase));

    private static void ApplySslFromQuery(NpgsqlConnectionStringBuilder csb, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;

            if (!kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                continue;

            csb.SslMode = kv[1].Trim().ToLowerInvariant() switch
            {
                "require" => SslMode.Require,
                "verify-ca" => SslMode.VerifyCA,
                "verify-full" => SslMode.VerifyFull,
                "prefer" => SslMode.Prefer,
                "disable" => SslMode.Disable,
                _ => csb.SslMode
            };
        }
    }

    private static bool DetectRenderDeployment() =>
        string.Equals(Environment.GetEnvironmentVariable("RENDER"), "true", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_SERVICE_ID"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));

}

