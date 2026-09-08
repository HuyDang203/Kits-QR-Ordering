using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Application.Integration;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Auth;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Dtos;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Mapping;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Services;

internal sealed class KiotVietOrderSyncService(
    IHttpClientFactory httpClientFactory,
    IOptions<KiotVietOptions> options,
    AppDbContext db,
    ILogger<KiotVietOrderSyncService> logger)
    : IKiotVietOrderSyncService
{
    private static readonly JsonSerializerOptions s_json = new() { PropertyNameCaseInsensitive = true };

    public async Task<KiotVietOrderPushResult> PushOrderAsync(
        KiotVietOutboxMessage message,
        JsonDocument payload,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;

        KiotVietOrderPayload? snapshot;
        try
        {
            snapshot = payload.RootElement.Deserialize<KiotVietOrderPayload>(s_json);
            if (snapshot is null)
                throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "KiotViet outbox {OutboxId}: payload snapshot deserialization failed.",
                message.Id);
            return new KiotVietOrderPushResult(false, null, null, $"Payload error: {ex.Message}");
        }

        var request = KvOrderMapper.Map(snapshot, opts);
        var client = httpClientFactory.CreateClient(KiotVietNamedHttpClients.Api);

        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, opts.OrdersPath);
            httpRequest.Content = JsonContent.Create(request, options: s_json);

            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var created = JsonSerializer.Deserialize<KvCreateOrderResponse>(body, s_json);
                var kvId = created?.Id.ToString(CultureInfo.InvariantCulture);

                logger.LogInformation(
                    "KiotViet order push succeeded: outboxId={OutboxId} annap={OrderId} kvId={KvId} latencyMs={Latency}",
                    message.Id, message.OrderId, kvId, sw.ElapsedMilliseconds);

                await WriteSyncLogAsync(
                    message.OrderId, "OrderSubmitted",
                    success: true,
                    kvReference: kvId,
                    httpStatus: (int)response.StatusCode,
                    durationMs: sw.ElapsedMilliseconds,
                    failureReason: null,
                    cancellationToken);

                return new KiotVietOrderPushResult(true, kvId, (int)response.StatusCode, null);
            }

            var detail = ExtractErrorDetail(body, (int)response.StatusCode);

            logger.LogWarning(
                "KiotViet order push failed: outboxId={OutboxId} annap={OrderId} http={Http} latencyMs={Latency} detail={Detail}",
                message.Id, message.OrderId, (int)response.StatusCode, sw.ElapsedMilliseconds,
                detail.Length > 200 ? detail[..200] : detail);

            await WriteSyncLogAsync(
                message.OrderId, "OrderSubmitted",
                success: false,
                kvReference: null,
                httpStatus: (int)response.StatusCode,
                durationMs: sw.ElapsedMilliseconds,
                failureReason: detail.Length > 4000 ? detail[..4000] : detail,
                cancellationToken);

            return new KiotVietOrderPushResult(false, null, (int)response.StatusCode, detail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new KiotVietOrderPushResult(false, null, null, "Cancelled.");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            logger.LogWarning(ex,
                "KiotViet network error: outboxId={OutboxId} annap={OrderId} latencyMs={Latency}",
                message.Id, message.OrderId, sw.ElapsedMilliseconds);
            await WriteSyncLogAsync(
                message.OrderId, "OrderSubmitted",
                success: false,
                kvReference: null,
                httpStatus: null,
                durationMs: sw.ElapsedMilliseconds,
                failureReason: $"Network: {ex.Message}",
                cancellationToken);
            return new KiotVietOrderPushResult(false, null, null, $"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            logger.LogWarning(ex,
                "KiotViet HTTP timeout: outboxId={OutboxId} annap={OrderId} latencyMs={Latency}",
                message.Id, message.OrderId, sw.ElapsedMilliseconds);
            await WriteSyncLogAsync(
                message.OrderId, "OrderSubmitted",
                success: false,
                kvReference: null,
                httpStatus: null,
                durationMs: sw.ElapsedMilliseconds,
                failureReason: "HTTP timeout",
                cancellationToken);
            return new KiotVietOrderPushResult(false, null, null, "HTTP timeout");
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task WriteSyncLogAsync(
        Guid orderId,
        string syncKind,
        bool success,
        string? kvReference,
        int? httpStatus,
        long durationMs,
        string? failureReason,
        CancellationToken ct)
    {
        try
        {
            var entry = new KiotVietSyncLog
            {
                SyncKind = syncKind,
                IsSuccess = success,
                ReferenceId = orderId.ToString("D"),
                KiotVietReference = kvReference,
                HttpStatusCode = httpStatus,
                DurationMs = durationMs,
                FailureReason = failureReason,
                OccurredAtUtc = DateTimeOffset.UtcNow
            };
            db.KiotVietSyncLogs.Add(entry);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write KiotVietSyncLog for orderId={OrderId}", orderId);
        }
    }

    private static string ExtractErrorDetail(string body, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
            return $"HTTP {statusCode} (empty body)";
        try
        {
            var err = JsonSerializer.Deserialize<KvApiErrorResponse>(body, s_json);
            if (err?.ResponseStatus?.Message is { Length: > 0 } m1) return m1;
            if (err?.Message is { Length: > 0 } m2) return m2;
        }
        catch { /* fall through to raw body */ }
        return body.Length <= 300 ? body : body[..300];
    }
}
