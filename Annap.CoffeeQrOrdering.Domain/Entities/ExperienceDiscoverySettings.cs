using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Singleton row (fixed id) for discovery ritual copy and pool rules.</summary>
public sealed class ExperienceDiscoverySettings : AuditableEntity
{
    /// <summary>When true, only seasonal highlights participate in the discovery pool.</summary>
    public bool SeasonalOnlyPool { get; set; }

    /// <summary>When false, seasonal highlights are omitted from discovery (unless seasonal-only mode forces them).</summary>
    public bool AllowSeasonalCups { get; set; } = true;

    /// <summary>Order signatures ahead of other eligible cups in weighted selection.</summary>
    public bool PreferSignaturesFirst { get; set; } = true;

    /// <summary>Guest may request another reveal after pacing rules.</summary>
    public bool AllowRerolls { get; set; } = true;

    /// <summary>1 (quiet) – 5 (adventurous); informs copy and future weighting.</summary>
    public int AdventureTone { get; set; } = 3;

    public string? CourierMoodCopy { get; set; }
    public string? FatigueCopyEvenLeg { get; set; }
    public string? FatigueCopyOddLeg { get; set; }

    /// <summary>Optional JSON for reroll pacing / A-B variants (future).</summary>
    public string? RerollPacingJson { get; set; }

    /// <summary>Reserved for future host-note templating.</summary>
    public string? RevealCopyNotes { get; set; }

    /// <summary>Optional JSON: desk title, three envelopes, CTAs, refusal lines, inside-letter lines, paper theme hints.</summary>
    public string? LetterRoomContentJson { get; set; }
}
