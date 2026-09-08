using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Lightweight trail for sommelier outcomes (Phase 9 learning loop).</summary>
public sealed class SommelierSuggestionFeedback : AuditableEntity
{
    public Guid SessionId { get; set; }

    public Guid MenuItemId { get; set; }

    /// <summary>accepted, ignored, ordered</summary>
    public string Outcome { get; set; } = "ignored";

    public string? MoodKey { get; set; }

    public string? RefinementKey { get; set; }
}
