using System.Diagnostics;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Development-only guard: logs shutdown duration and forces exit if host teardown hangs,
/// so DLL locks are not left behind after Ctrl+C or dotnet watch restart.
/// </summary>
internal static class DevelopmentShutdownWatchdog
{
    private static volatile bool _applicationStopped;
    private static Stopwatch? _shutdownStopwatch;

    public static void Register(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var lifetime = app.Lifetime;
        var forceExitAfter = TimeSpan.FromSeconds(18);

        lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"[dev] Host started. PID={Environment.ProcessId}");
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            _shutdownStopwatch = Stopwatch.StartNew();
            Console.WriteLine($"[dev] ApplicationStopping received. PID={Environment.ProcessId}");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(forceExitAfter).ConfigureAwait(false);
                    if (_applicationStopped)
                        return;

                    Console.WriteLine();
                    Console.WriteLine(
                        $"WARNING: Shutdown exceeded {forceExitAfter.TotalSeconds:0}s. " +
                        "Forcing process exit to release DLL locks (Domain/Application/Infrastructure).");
                    Console.WriteLine("If this repeats, run: .\\scripts\\dev-stop.ps1");
                    Console.WriteLine();
                    Environment.Exit(130);
                }
                catch
                {
                    // Best-effort watchdog only.
                }
            });
        });

        lifetime.ApplicationStopped.Register(() =>
        {
            _applicationStopped = true;
            var elapsed = _shutdownStopwatch?.Elapsed ?? TimeSpan.Zero;
            Console.WriteLine(
                $"[dev] ApplicationStopped. PID={Environment.ProcessId}; shutdown took {elapsed.TotalMilliseconds:0}ms");
        });
    }
}
