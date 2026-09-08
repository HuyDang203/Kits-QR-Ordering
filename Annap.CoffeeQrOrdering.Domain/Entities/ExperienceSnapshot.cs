using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Serialized experience workbench state (draft or published).</summary>
public sealed class ExperienceSnapshot : AuditableEntity
{
    /// <summary>0 = draft, 1 = published.</summary>
    public byte Kind { get; set; }

    /// <summary>Versioned JSON document for restore / history.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Optional curator note (never shown to guests).</summary>
    public string? HouseNote { get; set; }
}
