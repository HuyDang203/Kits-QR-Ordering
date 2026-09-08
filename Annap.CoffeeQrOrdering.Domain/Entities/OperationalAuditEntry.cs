namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Internal house log — not for guest display.</summary>
public sealed class OperationalAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>e.g. order.status, order.ownership, order.created</summary>
    public string ActionKind { get; set; } = null!;

    public string? Actor { get; set; }

    public Guid? OrderId { get; set; }

    /// <summary>Short factual line or compact JSON — no stack traces.</summary>
    public string Summary { get; set; } = null!;
}
