namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Intelligence;

public sealed record NamedCountVm(string Label, int Count);

public sealed record QuietMetricVm(string Line, string? Whisper);

public sealed record SparkBarVm(string Label, double Height01);

public sealed record TransitionVm(string FromHint, string ToHint, int Times);

public sealed record IntelligencePageVm(
    string HeroEyebrow,
    string HeroTitle,
    string HeroLead,
    IReadOnlyList<string> EditorialSummaries,
    RoomRhythmSectionVm RoomRhythm,
    TasteDirectionSectionVm TasteDirection,
    ServiceAtmosphereSectionVm ServiceAtmosphere,
    CupPerformanceSectionVm CupPerformance,
    SommelierSectionVm Sommelier);

public sealed record RoomRhythmSectionVm(
    string Narrative,
    IReadOnlyList<QuietMetricVm> Metrics,
    IReadOnlyList<SparkBarVm> HourSpark,
    int HourHistogramMax);

public sealed record TasteDirectionSectionVm(
    string Narrative,
    IReadOnlyList<NamedCountVm> MoodCounts,
    IReadOnlyList<NamedCountVm> RefinementCounts,
    IReadOnlyList<QuietMetricVm> QuietLines);

public sealed record ServiceAtmosphereSectionVm(
    string Narrative,
    IReadOnlyList<QuietMetricVm> Metrics);

public sealed record CupPerformanceSectionVm(
    string Narrative,
    IReadOnlyList<NamedCountVm> TopDrinks,
    IReadOnlyList<QuietMetricVm> QuietLines);

public sealed record SommelierSectionVm(
    string Narrative,
    IReadOnlyList<NamedCountVm> Outcomes,
    IReadOnlyList<QuietMetricVm> QuietLines,
    IReadOnlyList<TransitionVm> SensoryTransitions);
