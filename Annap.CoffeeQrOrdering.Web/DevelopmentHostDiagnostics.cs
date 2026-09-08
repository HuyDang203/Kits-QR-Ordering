using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Web;

internal static class DevelopmentHostDiagnostics
{
    private sealed record ProcessHint(int Pid, string Kind, string? CommandLine);

    private static IReadOnlyList<ProcessHint> FindStaleAnnapProcesses(int? excludePid = null)
    {
        var currentPid = excludePid ?? Environment.ProcessId;
        var parentPid = TryGetParentProcessId(currentPid);
        var excluded = new HashSet<int> { currentPid };
        if (parentPid > 0)
            excluded.Add(parentPid);

        return Process.GetProcessesByName("Annap.CoffeeQrOrdering.Web")
            .Where(p => !excluded.Contains(SafeId(p)))
            .Select(p => new ProcessHint(SafeId(p), "Annap.CoffeeQrOrdering.Web", null))
            .Concat(FindDotnetAnnapProcesses(currentPid))
            .Where(p => p.Pid > 0 && !excluded.Contains(p.Pid))
            .Where(p => p.Kind != "dotnet" || PortsOwnedBy(p.Pid).Count > 0 || !string.IsNullOrWhiteSpace(p.CommandLine))
            .GroupBy(p => p.Pid)
            .Select(g => g.First())
            .OrderBy(p => p.Pid)
            .ToList();
    }

    private static int TryGetParentProcessId(int pid)
    {
        if (pid <= 0 || !OperatingSystem.IsWindows())
            return -1;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                "(Get-CimInstance Win32_Process -Filter \"ProcessId = " + pid +
                "\").ParentProcessId");

            using var proc = Process.Start(startInfo);
            if (proc is null)
                return -1;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(1500);
            return int.TryParse(output, out var parent) ? parent : -1;
        }
        catch
        {
            return -1;
        }
    }

    public static void PrintStaleProcessHintsIfNeeded(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return;

        var hints = FindStaleAnnapProcesses();
        if (hints.Count == 0)
            return;

        PrintStaleProcessReport(hints, "Another ANNAP instance may still be running.");
    }

    /// <summary>
    /// Fail fast when another dev host is already alive and would cause MSB3021/MSB3027 on rebuild.
    /// Set ANNAP_DEV_ALLOW_PARALLEL=1 to bypass (not recommended).
    /// </summary>
    public static void AssertNoConcurrentDevHosts(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return;

        if (string.Equals(
                Environment.GetEnvironmentVariable("ANNAP_DEV_ALLOW_PARALLEL"),
                "1",
                StringComparison.Ordinal))
            return;

        var hints = FindStaleAnnapProcesses();
        if (hints.Count == 0)
            return;

        var watchCount = hints.Count(h =>
            h.CommandLine?.Contains("watch", StringComparison.OrdinalIgnoreCase) == true);
        if (watchCount > 1)
        {
            Console.WriteLine();
            Console.WriteLine("ERROR: Multiple dotnet watch sessions detected for Annap.CoffeeQrOrdering.Web.");
            Console.WriteLine("Use a single watcher: .\\scripts\\dev-restart.ps1 -Watch");
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("ERROR: Another ANNAP development host is already running.");
        Console.WriteLine("It locks Domain.dll, Application.dll, and Infrastructure.dll during rebuild.");
        Console.WriteLine();
        PrintStaleProcessReport(hints, "Running processes:");
        Console.WriteLine("Stop the existing host, then start again:");
        Console.WriteLine("  .\\scripts\\dev-stop.ps1");
        Console.WriteLine("  .\\scripts\\dev-restart.ps1");
        Console.WriteLine();
        Console.WriteLine("Or full clean reset:");
        Console.WriteLine("  .\\scripts\\dev-reset.ps1");
        Console.WriteLine();
        Environment.Exit(1);
    }

    public static void PrintPreRunDiagnostics(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var pid = Environment.ProcessId;
        var env = app.Environment.EnvironmentName;
        var urls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "(none configured)";
        var aspUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "(not set)";
        var stale = FindStaleAnnapProcesses(pid);

        Console.WriteLine();
        Console.WriteLine("ANNAP DEVELOPMENT RUNTIME");
        Console.WriteLine("-------------------------");
        Console.WriteLine($"  PID:              {pid}");
        Console.WriteLine($"  Environment:      {env}");
        Console.WriteLine($"  Kestrel URLs:     {urls}");
        Console.WriteLine($"  ASPNETCORE_URLS:  {aspUrls}");
        Console.WriteLine($"  Other ANNAP PIDs: {(stale.Count == 0 ? "none detected" : string.Join(", ", stale.Select(s => $"{s.Pid} ({s.Kind})")))}");

        if (stale.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  WARNING: A previous ANNAP host may still be alive.");
            Console.WriteLine("  That process locks Domain.dll / Application.dll / Infrastructure.dll");
            Console.WriteLine("  and causes MSB3027 during dotnet build or hot reload.");
            Console.WriteLine();
            Console.WriteLine("  Stop stale host:");
            Console.WriteLine("    .\\scripts\\dev-stop.ps1");
            Console.WriteLine("  Restart after C#/Razor changes:");
            Console.WriteLine("    .\\scripts\\dev-restart.ps1");
            Console.WriteLine();
            PrintStaleProcessReport(stale, "Stale process details:");
        }

        var watchSessions = stale.Count(s =>
            s.CommandLine?.Contains("watch", StringComparison.OrdinalIgnoreCase) == true);
        if (watchSessions > 0)
            Console.WriteLine($"  dotnet watch sessions (other PIDs): {watchSessions}");

        Console.WriteLine();
        Console.WriteLine("  Stop cleanly: Ctrl+C in this terminal (do not close the terminal window).");
        Console.WriteLine("  Stop from another terminal: .\\scripts\\dev-stop.ps1");
        Console.WriteLine("  Rebuild + run: .\\scripts\\dev-restart.ps1");
        Console.WriteLine();
    }

    private static void PrintStaleProcessReport(IReadOnlyList<ProcessHint> hints, string heading)
    {
        Console.WriteLine();
        Console.WriteLine(heading);
        foreach (var p in hints)
        {
            Console.WriteLine($"  PID {p.Pid} ({p.Kind})");
            var ports = PortsOwnedBy(p.Pid);
            if (ports.Count > 0)
                Console.WriteLine($"    Listening ports: {string.Join(", ", ports)}");
            if (!string.IsNullOrWhiteSpace(p.CommandLine))
                Console.WriteLine($"    Command: {Truncate(p.CommandLine, 160)}");
            Console.WriteLine($"    Terminate: taskkill /PID {p.Pid} /T /F");
        }

        Console.WriteLine();
    }

    public static void PrintPortOwnerHint(int port)
    {
        var owners = ProcessIdsListeningOn(port);
        if (owners.Count == 0)
            return;

        Console.WriteLine($"  Port {port} is currently owned by PID(s): {string.Join(", ", owners)}");
        foreach (var pid in owners)
            Console.WriteLine($"    taskkill /PID {pid} /F");
        Console.WriteLine();
    }

    public static IReadOnlyList<string> PortsOwnedBy(int pid)
    {
        if (pid <= 0 || !OperatingSystem.IsWindows())
            return [];

        try
        {
            var output = RunNetstat();
            var suffix = " " + pid.ToString();
            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.EndsWith(suffix, StringComparison.Ordinal))
                .Where(line => line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                .Select(ExtractLocalPort)
                .Where(port => !string.IsNullOrWhiteSpace(port))
                .Distinct()
                .OrderBy(x => x)
                .ToList()!;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<int> ProcessIdsListeningOn(int port)
    {
        if (port <= 0 || !OperatingSystem.IsWindows())
            return [];

        try
        {
            var marker = ":" + port.ToString();
            return RunNetstat()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                .Where(line => line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                .Select(ExtractPid)
                .Where(pid => pid > 0 && pid != Environment.ProcessId)
                .Distinct()
                .OrderBy(pid => pid)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<ProcessHint> FindDotnetAnnapProcesses(int currentPid)
    {
        if (!OperatingSystem.IsWindows())
            return [];

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                "$current = " + currentPid +
                "; Get-CimInstance Win32_Process -Filter \"name = 'dotnet.exe'\" " +
                "| Where-Object { $_.ProcessId -ne $current -and $_.CommandLine -like '*Annap.CoffeeQrOrdering.Web*' } " +
                "| ForEach-Object { \"$($_.ProcessId)|dotnet|$($_.CommandLine)\" }");

            using var proc = Process.Start(startInfo);

            if (proc is null)
                return [];

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);
            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('|', 3, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length >= 2 && int.TryParse(parts[0], out _))
                .Select(parts => new ProcessHint(
                    int.Parse(parts[0]),
                    parts[1],
                    parts.Length > 2 ? parts[2] : null))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..max] + "...";
    }

    private static string RunNetstat()
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano -p tcp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        });

        if (proc is null)
            return "";

        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(1500);
        return output;
    }

    private static string? ExtractLocalPort(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return null;

        var local = parts[1];
        var colon = local.LastIndexOf(':');
        if (colon < 0 || colon == local.Length - 1)
            return null;

        return local[(colon + 1)..];
    }

    private static int ExtractPid(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && int.TryParse(parts[^1], out var pid) ? pid : -1;
    }

    private static int SafeId(Process p)
    {
        try { return p.Id; }
        catch { return -1; }
    }
}
