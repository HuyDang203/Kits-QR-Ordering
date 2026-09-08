using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;

namespace Annap.CoffeeQrOrdering.Application.Abstractions;

/// <summary>Pushes a single order snapshot to KiotViet (outbox worker only).</summary>
public interface IKiotVietOrderSyncService
{
    /// <summary>
    /// POST mapped order to KiotViet. Returns remote order id when API returns success.
    /// Throws only for unrecoverable programming errors — HTTP failures should be handled by caller.
    /// </summary>
    Task<KiotVietOrderPushResult> PushOrderAsync(
        KiotVietOutboxMessage message,
        JsonDocument payload,
        CancellationToken cancellationToken);
}

public sealed record KiotVietOrderPushResult(bool Success, string? KiotVietOrderId, int? HttpStatus, string? ErrorDetail);
