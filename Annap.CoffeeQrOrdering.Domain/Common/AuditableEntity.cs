namespace Annap.CoffeeQrOrdering.Domain.Common;

public abstract class AuditableEntity : EntityBase
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

