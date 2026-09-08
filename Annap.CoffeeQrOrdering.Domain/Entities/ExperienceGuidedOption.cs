using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class ExperienceGuidedOption : AuditableEntity
{
    public Guid QuestionId { get; set; }
    public ExperienceGuidedQuestion Question { get; set; } = null!;

    public string ExternalKey { get; set; } = "";
    public string Label { get; set; } = "";

    /// <summary>Native English label — independent copy, not a translation of <see cref="Label"/>.</summary>
    public string? LabelEn { get; set; }

    /// <summary>Richer copy for CMS / future guest detail (Vietnamese).</summary>
    public string? Description { get; set; }

    /// <summary>Native English description / guest reflection.</summary>
    public string? DescriptionEn { get; set; }

    public string? Subline { get; set; }

    /// <summary>Native English subline / emotional fragment.</summary>
    public string? SublineEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Optional mood axis key for intelligence / future scoring.</summary>
    public string? MoodKey { get; set; }

    /// <summary>Optional refinement key from the sommelier ritual.</summary>
    public string? RefinementKey { get; set; }

    /// <summary>JSON array or map of flavor tags for search / tuning.</summary>
    public string? FlavorTagsJson { get; set; }

    /// <summary>Scales affinity contribution for this answer (typically 0.25–4).</summary>
    public decimal WeightMultiplier { get; set; } = 1m;

    /// <summary>JSON hints for sensory profile (caffeine, sweetness, acidity, etc.).</summary>
    public string SensoryProfileJson { get; set; } = "{}";

    public List<ExperienceGuidedAffinity> Affinities { get; set; } = [];
}
