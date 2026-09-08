using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;

/// <summary>Transactional outbox row: one integration attempt chain per submitted order event.</summary>
public sealed class KiotVietOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>Event discriminator, e.g. OrderSubmitted, OrderCancelled.</summary>
    public string EventType { get; set; } = null!;

    /// <summary>JSON snapshot at enqueue time for deterministic replay.</summary>
    public string Payload { get; set; } = null!;

    public KiotVietOutboxStatus Status { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string? KiotVietOrderId { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
