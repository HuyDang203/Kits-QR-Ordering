namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Tracks development-only fire-and-forget work so host shutdown can wait briefly
/// instead of leaving orphaned thread-pool tasks holding scoped services and DLL locks.
/// </summary>
internal static class DevelopmentBackgroundTaskTracker
{
    private sealed record Entry(string Name, Task Task);

    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = [];

    public static void Start(
        string name,
        IHostApplicationLifetime lifetime,
        Func<CancellationToken, Task> work)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        var task = Task.Run(async () =>
        {
            try
            {
                await work(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                // Expected during Ctrl+C / dotnet watch restart.
            }
            catch (Exception)
            {
                // Callers log their own failures; never let this bubble into host teardown.
            }
        }, cts.Token);

        lock (Gate)
            Entries.Add(new Entry(name, task));
    }

    public static IReadOnlyList<string> PendingTaskNames()
    {
        lock (Gate)
        {
            return Entries
                .Where(e => !e.Task.IsCompleted)
                .Select(e => e.Name)
                .ToList();
        }
    }

    public static void WaitForCompletion(TimeSpan timeout)
    {
        Task[] tasks;
        lock (Gate)
            tasks = Entries.Select(e => e.Task).ToArray();

        if (tasks.Length == 0)
            return;

        try
        {
            Task.WaitAll(tasks, timeout);
        }
        catch (AggregateException)
        {
            // Best-effort drain; host teardown continues.
        }
    }
}
