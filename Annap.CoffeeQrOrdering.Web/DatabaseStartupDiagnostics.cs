namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Lightweight startup DB state for console banner and ops visibility (set during bootstrap only).
/// </summary>
internal static class DatabaseStartupDiagnostics
{
    private static string _connectionDisplay = "(not parsed)";
    private static bool _waitFinished;
    private static bool _connectOk;
    private static bool? _migrationsApplied;
    private static long _elapsedMs;

    public static void Reset()
    {
        _connectionDisplay = "(not parsed)";
        _waitFinished = false;
        _connectOk = false;
        _migrationsApplied = null;
        _elapsedMs = 0;
    }

    public static void SetConnectionDisplay(string display) => _connectionDisplay = display ?? "";

    public static void MarkWaitFinished(bool connectOk, long elapsedMs, bool? migrationsApplied)
    {
        _waitFinished = true;
        _connectOk = connectOk;
        _elapsedMs = elapsedMs;
        _migrationsApplied = migrationsApplied;
    }

    public static void MarkMigrationsApplied(bool applied)
    {
        _waitFinished = true;
        _connectOk = true;
        _migrationsApplied = applied;
    }

    public static string ConnectionDisplay => _connectionDisplay;

    public static string SummaryForBanner()
    {
        if (!_waitFinished)
            return "bootstrap running or skipped — see log category Startup.DbBootstrap";
        var mig = _migrationsApplied switch
        {
            true => "applied",
            false => "failed",
            _ => "n/a (wait failed or skipped)"
        };
        return $"host {_connectionDisplay} · connect {(_connectOk ? "ok" : "no")} · migrations {mig} · {_elapsedMs}ms";
    }
}
