using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Singleton CMS row: which guest arrival paths appear on the seated homepage.</summary>
public sealed class HomepageExperienceSettings : AuditableEntity
{
    /// <summary>Table ritual — <c>data-ge-flow="group"</c>.</summary>
    public bool IsGroupEnabled { get; set; } = true;

    /// <summary>Barista concierge / solo path — <c>data-ge-flow="sommelier"</c>.</summary>
    public bool IsSoloEnabled { get; set; } = true;

    /// <summary>Sealed surprise path — <c>data-ge-flow="discovery"</c>.</summary>
    public bool IsSommelierEnabled { get; set; } = true;
}
