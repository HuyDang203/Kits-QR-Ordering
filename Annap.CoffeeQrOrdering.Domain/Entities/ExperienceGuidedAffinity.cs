using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Weighted link from a guided answer to a menu cup (CMS-tunable scoring).</summary>
public sealed class ExperienceGuidedAffinity : AuditableEntity
{
    public Guid OptionId { get; set; }
    public ExperienceGuidedOption Option { get; set; } = null!;

    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    /// <summary>Relative boost (typically 0–2; scaled in the recommendation engine).</summary>
    public decimal Weight { get; set; } = 0.5m;
}
