using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Audit line when the house ritual was published for guests.</summary>
public sealed class ExperiencePublishRecord : AuditableEntity
{
    public Guid SnapshotId { get; set; }
    public ExperienceSnapshot Snapshot { get; set; } = null!;
}
