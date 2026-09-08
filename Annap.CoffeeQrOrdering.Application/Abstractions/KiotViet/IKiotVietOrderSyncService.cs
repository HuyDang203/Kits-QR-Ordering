namespace Annap.CoffeeQrOrdering.Application.Abstractions.KiotViet;

/// <summary>Outcome of a single attempt to push one outbox order snapshot to KiotViet.</summary>
public sealed record KiotVietOrderPushResult(bool Success, int? HttpStatus, string? KiotVietOrderId, string? ErrorMessage);

/// <summary>Maps and sends one order snapshot to KiotViet (Infrastructure implementation).</summary>
public interface IKiotVietOrderSyncService
{
    Task<KiotVietOrderPushResult> PushOrderPayloadAsync(string payloadJson, CancellationToken cancellationToken);
}
