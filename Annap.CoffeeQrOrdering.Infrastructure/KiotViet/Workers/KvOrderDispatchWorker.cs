using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Workers;

public sealed class KvOrderDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KvOrderDispatchWorker> _logger;

    public KvOrderDispatchWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<KvOrderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KvOrderDispatchWorker started.");

        try
        {
            await RecoverStuckProcessingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("KvOrderDispatchWorker cancelled before recovery finished.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSeconds;
            try
            {
                intervalSeconds = await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KvOrderDispatchWorker tick threw unexpectedly.");
                intervalSeconds = 10;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("KvOrderDispatchWorker stopped.");
    }

    private async Task RecoverStuckProcessingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<KiotVietOptions>>().Value;

            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-opts.StuckProcessingSeconds);
            var now = DateTimeOffset.UtcNow;

            var recovered = await db.KiotVietOutboxMessages
                .Where(m => m.Status == KiotVietOutboxStatus.Processing && m.UpdatedAtUtc < cutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, KiotVietOutboxStatus.Pending)
                    .SetProperty(m => m.UpdatedAtUtc, now),
                    ct)
                .ConfigureAwait(false);

            if (recovered > 0)
                _logger.LogWarning(
                    "KvOrderDispatchWorker recovered {Count} stuck-Processing outbox rows on startup.",
                    recovered);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KvOrderDispatchWorker stuck-processing recovery failed; continuing.");
        }
    }

    // Returns the configured dispatch interval so the caller can delay correctly.
    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        int maxConcurrent;
        int intervalSeconds;
        List<(Guid Id, Guid OrderId, string Payload, int RetryCount)> batch;

        // Claim phase: short-lived scope — lock rows, set to Processing, commit, then release.
        // Collected tuples are plain value types; no EF tracking survives scope disposal.
        using (var claimScope = _scopeFactory.CreateScope())
        {
            var db = claimScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var opts = claimScope.ServiceProvider.GetRequiredService<IOptions<KiotVietOptions>>().Value;
            maxConcurrent = opts.DispatchMaxConcurrent;
            intervalSeconds = opts.OrderDispatchIntervalSeconds;

            if (!opts.IsEnabled)
                return intervalSeconds;

            var now = DateTimeOffset.UtcNow;
            var batchSize = opts.DispatchBatchSize;

            List<KiotVietOutboxMessage> claimed;
            await using (var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    claimed = await db.KiotVietOutboxMessages
                        .FromSql($"""
                            SELECT * FROM kiotviet_outbox_messages
                            WHERE "Status" IN (0, 3)
                            AND ("NextRetryAtUtc" IS NULL OR "NextRetryAtUtc" <= {now})
                            ORDER BY "CreatedAtUtc"
                            LIMIT {batchSize}
                            FOR UPDATE SKIP LOCKED
                            """)
                        .AsTracking()
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    if (claimed.Count == 0)
                    {
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        return intervalSeconds;
                    }

                    var claimTime = DateTimeOffset.UtcNow;
                    foreach (var m in claimed)
                    {
                        m.Status = KiotVietOutboxStatus.Processing;
                        m.UpdatedAtUtc = claimTime;
                    }

                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }

            batch = claimed
                .Select(m => (m.Id, m.OrderId, m.Payload, m.RetryCount))
                .ToList();
        }

        _logger.LogInformation(
            "KvOrderDispatchWorker claimed {Count} outbox rows for dispatch.", batch.Count);

        // Process phase: each message gets its own DI scope (independent DbContext + SyncService).
        // This prevents concurrent SaveChangesAsync calls on a shared DbContext.
        using var sem = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        var tasks = batch
            .Select(item => ProcessOneAsync(item.Id, item.OrderId, item.Payload, item.RetryCount, sem, ct))
            .ToList();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }

        return intervalSeconds;
    }

    private async Task ProcessOneAsync(
        Guid id,
        Guid orderId,
        string payloadJson,
        int prevRetryCount,
        SemaphoreSlim sem,
        CancellationToken ct)
    {
        // Wrap the entire method so cancellation before semaphore acquisition also completes cleanly.
        var acquired = false;
        try
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            acquired = true;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var syncService = scope.ServiceProvider.GetRequiredService<IKiotVietOrderSyncService>();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<KiotVietOptions>>().Value;

            // Lightweight carrier — not EF-tracked; we persist via ExecuteUpdateAsync.
            var carrier = new KiotVietOutboxMessage
            {
                Id = id,
                OrderId = orderId,
                Payload = payloadJson,
                RetryCount = prevRetryCount
            };

            KiotVietOrderPushResult result;
            try
            {
                using var payloadDoc = JsonDocument.Parse(payloadJson);
                result = await syncService.PushOrderAsync(carrier, payloadDoc, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await ResetToPendingAsync(db, id).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "KvOrderDispatchWorker unhandled error in sync service: outboxId={OutboxId}", id);
                await ResetToPendingAsync(db, id).ConfigureAwait(false);
                return;
            }

            ApplyResult(carrier, result, opts);
            await PersistResultAsync(db, carrier).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelled before semaphore or during processing — row stays Processing;
            // stuck-recovery resets it on next startup.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "KvOrderDispatchWorker failed for outboxId={OutboxId}. " +
                "Row remains Processing; stuck-recovery will reset it on next startup.", id);
        }
        finally
        {
            if (acquired) sem.Release();
        }
    }

    private static async Task ResetToPendingAsync(AppDbContext db, Guid id)
    {
        try
        {
            await db.KiotVietOutboxMessages
                .Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, KiotVietOutboxStatus.Pending)
                    .SetProperty(m => m.UpdatedAtUtc, DateTimeOffset.UtcNow),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch { /* best-effort; stuck-recovery handles it on next startup */ }
    }

    private static async Task PersistResultAsync(
        AppDbContext db,
        KiotVietOutboxMessage carrier)
    {
        var status = carrier.Status;
        var retryCount = carrier.RetryCount;
        var kvOrderId = carrier.KiotVietOrderId;
        var failureReason = carrier.FailureReason;
        var nextRetryAtUtc = carrier.NextRetryAtUtc;
        var processedAtUtc = carrier.ProcessedAtUtc;
        var updatedAtUtc = carrier.UpdatedAtUtc;
        var id = carrier.Id;

        await db.KiotVietOutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, status)
                    .SetProperty(m => m.RetryCount, retryCount)
                    .SetProperty(m => m.KiotVietOrderId, kvOrderId)
                    .SetProperty(m => m.FailureReason, failureReason)
                    .SetProperty(m => m.NextRetryAtUtc, nextRetryAtUtc)
                    .SetProperty(m => m.ProcessedAtUtc, processedAtUtc)
                    .SetProperty(m => m.UpdatedAtUtc, updatedAtUtc),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private void ApplyResult(
        KiotVietOutboxMessage carrier,
        KiotVietOrderPushResult result,
        KiotVietOptions opts)
    {
        var now = DateTimeOffset.UtcNow;
        carrier.UpdatedAtUtc = now;

        if (result.Success)
        {
            carrier.Status = KiotVietOutboxStatus.Succeeded;
            carrier.ProcessedAtUtc = now;
            carrier.KiotVietOrderId = result.KiotVietOrderId;
            carrier.FailureReason = null;
            carrier.NextRetryAtUtc = null;
            return;
        }

        carrier.RetryCount++;
        carrier.FailureReason = result.ErrorDetail?.Length > 4000
            ? result.ErrorDetail[..4000]
            : result.ErrorDetail;

        if (carrier.RetryCount > opts.MaxDispatchRetries || !IsRetryable(result.HttpStatus))
        {
            carrier.Status = KiotVietOutboxStatus.DeadLettered;
            carrier.NextRetryAtUtc = null;
            _logger.LogWarning(
                "KiotViet outbox dead-lettered: outboxId={OutboxId} annap={OrderId} retryCount={Retry} http={Http}",
                carrier.Id, carrier.OrderId, carrier.RetryCount, result.HttpStatus);
        }
        else
        {
            carrier.Status = KiotVietOutboxStatus.Failed;
            carrier.NextRetryAtUtc = DateTimeOffset.UtcNow.Add(NextRetryDelay(carrier.RetryCount));
            _logger.LogInformation(
                "KiotViet outbox retry scheduled: outboxId={OutboxId} annap={OrderId} attempt={Retry} nextUtc={NextRetry}",
                carrier.Id, carrier.OrderId, carrier.RetryCount, carrier.NextRetryAtUtc);
        }
    }

    private static bool IsRetryable(int? httpStatus) => httpStatus switch
    {
        null => true,      // network error or timeout
        401 => true,       // token may rotate on next attempt
        403 => true,       // transient permission issue
        429 => true,       // rate limited
        >= 500 => true,    // KiotViet server error
        _ => false         // 400/404/422 — data error, won't fix with retries
    };

    private static TimeSpan NextRetryDelay(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(3),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(4)
    };
}
