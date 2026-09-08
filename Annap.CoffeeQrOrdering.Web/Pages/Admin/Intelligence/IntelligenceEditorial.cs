namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Intelligence;

internal static class IntelligenceEditorial
{
    public static string ComposeTasteNarrative(
        IReadOnlyList<(string Key, int Cnt)> moodRows,
        IReadOnlyList<(string Key, int Cnt)> refinementRows,
        int warmOutcomes,
        int ignored)
    {
        _ = ignored;
        if (moodRows.Count == 0 && refinementRows.Count == 0)
            return "Taste whispers are still gathering — when moods and refinements appear, this paragraph will read like a sommelier's diary, not a chart.";

        var moodHint = moodRows.Count > 0 ? moodRows[0].Key : null;
        var refHint = refinementRows.Count > 0 ? refinementRows[0].Key : null;
        var m = string.IsNullOrEmpty(moodHint) ? "" : HumanizeKey(moodHint).ToLowerInvariant();
        var r = string.IsNullOrEmpty(refHint) ? "" : HumanizeKey(refHint).ToLowerInvariant();

        if (!string.IsNullOrEmpty(m) && !string.IsNullOrEmpty(r))
            return $"Guests leaned toward {m}, while refinements often asked for {r} — a quiet dialogue between first impulse and second thought.";
        if (!string.IsNullOrEmpty(m))
            return $"The emotional compass settled most often on {m} — a single mood carrying more than one evening.";
        if (!string.IsNullOrEmpty(r))
            return $"Refinements kept returning to {r} — the room refining itself in small, repeated gestures.";
        return warmOutcomes > 0
            ? "Accepted paths and quiet orders outnumber blunt declines — curiosity is winning softly."
            : "The palette is still forming; a few more services will bring this sentence into focus.";
    }

    public static string ComposeServiceNarrative(double? medianAll, int openOrders, int gentleDelays, string? smoothestDayKey)
    {
        var parts = new List<string>();
        if (medianAll is > 0)
            parts.Add($"Handoffs are averaging near {medianAll:0} minutes from first mark to served — a hospitable tempo, not a race.");
        if (openOrders > 0 && openOrders <= 3)
            parts.Add("Only a handful of tickets are still in motion — the floor feels breathable.");
        else if (openOrders > 8)
            parts.Add("Several tickets are still unfolding together — the line is full, but not frantic.");
        else if (openOrders == 0)
            parts.Add("Between rushes, the room is resting — no open tickets at this breath.");

        if (gentleDelays == 0 && medianAll is > 0)
            parts.Add("Nothing in the last month asked for a sharp breath; lengthening moments stayed rare.");

        if (!string.IsNullOrEmpty(smoothestDayKey))
            parts.Add($"{smoothestDayKey}s carried the lightest stretch — smooth shifts deserve a quiet nod.");

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Service atmosphere will clarify as completed tickets accumulate — the observatory is patient.";
    }

    public static string ComposeCupNarrative(
        IReadOnlyList<(string Name, int Cups)> topDrinks,
        double sigPct,
        double seaPct,
        int totalFeedback,
        int ordered)
    {
        if (topDrinks.Count == 0)
            return "Cup stories wait on the first orders of the month — the line sheet is ready when the room is.";

        var lead = topDrinks[0].Name;
        var tail = totalFeedback > 0 && ordered > 0
            ? $" Sommelier suggestions that became orders: {ordered} quiet yeses in the last month."
            : "";
        return $"{lead} led the pour — familiar, trusted, unforced.{tail} Signature presence sat near {sigPct:0}% of tickets; seasonal highlights near {seaPct:0}% — small tides, honestly told.";
    }

    public static string ComposeSommelierNarrative(int ordered, int accepted, int ignored, int transitionCount)
    {
        var warm = ordered + accepted;
        if (warm == 0 && ignored == 0)
            return "The invisible sommelier is listening — when feedback arrives, this space honours acceptances, refinements, and gentle declines alike.";

        var denom = ignored + warm;
        var ratio = denom > 0 ? 100.0 * warm / denom : 0;
        var t = transitionCount > 0
            ? $" {transitionCount} sensory handoffs stood out — one cup suggesting the next without haste."
            : "";
        return
            $"About {ratio:0}% of recorded responses leaned toward acceptance or order rather than a quiet decline — hospitality, not conversion.{t}";
    }

    public static IReadOnlyList<string> ComposeSummaries(
        IReadOnlyList<KeyValuePair<string, int>> topBands,
        IReadOnlyList<(string Key, int Cnt)> moodRows,
        IReadOnlyList<(string Key, int Cnt)> refinementRows,
        double? medianAll,
        double sigPct,
        double? recent7,
        double? prev7)
    {
        var list = new List<string>();
        if (topBands.Count > 0 && topBands[0].Value > 0)
            list.Add(
                $"The room favoured {topBands[0].Key.ToLowerInvariant()} — when light shifts, this sentence will shift with it.");

        if (moodRows.Count >= 2)
            list.Add(
                $"{HumanizeKey(moodRows[0].Key)} and {HumanizeKey(moodRows[1].Key)} traded places at the emotional centre — a soft pendulum, not a trend line.");

        if (refinementRows.Count > 0)
            list.Add(
                $"Refinements often asked for {HumanizeKey(refinementRows[0].Key).ToLowerInvariant()} — the bar heard guests thinking twice, kindly.");

        if (medianAll is > 10 && recent7 is > 0 && prev7 is > 0 && recent7 > prev7 * 1.08)
            list.Add("The last week asked for a touch more time at the bar than the week before — not alarm, only atmosphere.");
        else if (medianAll is > 10 && recent7 is > 0 && prev7 is > 0 && recent7 < prev7 * 0.92)
            list.Add("Handoffs grew lighter week over week — the room found a slightly swifter breath.");

        if (sigPct >= 25)
            list.Add("Signature cups kept good company — the house spotlight felt present without shouting.");

        if (list.Count == 0)
            list.Add("The observatory is warm and waiting — a few more services, and the room will speak in full sentences.");

        return list;
    }

    public static string DescribePacing(double? recent7, double? prev7)
    {
        if (recent7 is null or <= 0 || prev7 is null or <= 0) return "";
        if (recent7 > prev7 * 1.08)
            return "Compared to the week prior, tickets took a slightly longer breath — worth noticing with kindness, not metrics.";
        if (recent7 < prev7 * 0.92)
            return "Compared to the week prior, the line moved with a touch more ease — a soft exhale at the pass.";
        return "Week-over-week pacing held steady — the room kept its temperament.";
    }

    public static string HumanizeKey(string key) =>
        string.IsNullOrWhiteSpace(key) ? "—" : key.Replace('_', ' ').Trim();
}
