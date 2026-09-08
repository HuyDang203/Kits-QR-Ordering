using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Curated signature rail for the seated guest path (max 4 slots).</summary>
public sealed class ExperienceSignatureSlot : AuditableEntity
{
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    /// <summary>0-based display order on the strip.</summary>
    public int SortOrder { get; set; }

    /// <summary>When false, slot is hidden from the group guest rail but kept for curation.</summary>
    public bool IsActive { get; set; } = true;

    public bool IsSpotlight { get; set; }

    /// <summary>When true, seasonal kicker may appear on this slot in guest UI.</summary>
    public bool SeasonalSpotlightEnabled { get; set; }

    public string? EditorialKicker { get; set; }
    public string? EditorialBody { get; set; }
}
