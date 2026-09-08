using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>
/// A physical service table (QR identity). <see cref="PublicSlug"/> is the short-URL token (e.g. annap-t12).
/// </summary>
public sealed class VenueTable : EntityBase
{
    /// <summary>Venue or location code for future multi-site routing (lowercase, e.g. annap).</summary>
    public string VenueCode { get; set; } = "annap";

    /// <summary>Human-facing code shown on floor and staff boards (e.g. T12).</summary>
    public string DisplayCode { get; set; } = null!;

    /// <summary>Unique slug for /t/{slug} (lowercase, e.g. annap-t12).</summary>
    public string PublicSlug { get; set; } = null!;

    /// <summary>Optional softer label for guests (e.g. Garden · 12).</summary>
    public string? DisplayLabel { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>KiotViet table id as returned by POS (string form); optional — unmapped tables still sync with note-only routing.</summary>
    public string? KiotVietTableId { get; set; }

    /// <summary>Optional branch override; when null, worker uses global <c>KiotViet:BranchId</c>.</summary>
    public int? KiotVietBranchId { get; set; }
}
