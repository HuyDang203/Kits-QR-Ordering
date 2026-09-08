using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Intelligence;

internal static class IntelligenceDataLoader
{
    private static readonly (string Label, int Start, int End)[] DayParts =
    [
        ("Early service", 6, 10),
        ("Mid-morning", 11, 13),
        ("Lunch shoulder", 14, 16),
        ("Afternoon pause", 17, 18),
        ("Golden hour", 19, 21),
        ("Late cups", 22, 23),
        ("Quiet night", 0, 5)
    ];

    public static async Task<IntelligencePageVm> LoadAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var since30 = now.AddDays(-30);
        var since14 = now.AddDays(-14);
        var since7 = now.AddDays(-7);
        var prev7Start = now.AddDays(-14);
        var prev7End = now.AddDays(-7);

        var created14 = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAtUtc >= since14)
            .Select(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        var histogram = new int[24];
        foreach (var t in created14)
        {
            var h = t.ToLocalTime().Hour;
            if (h is >= 0 and <= 23)
                histogram[h]++;
        }

        var histMax = Math.Max(1, histogram.Max());

        var bandScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, start, end) in DayParts)
        {
            var sum = 0;
            for (var h = start; h <= end; h++)
                sum += histogram[Math.Clamp(h, 0, 23)];
            bandScores[label] = sum;
        }

        var topBands = bandScores.OrderByDescending(kv => kv.Value).Take(2).ToList();
        var bandNarrative = topBands.Count == 0 || topBands[0].Value == 0
            ? "The last fortnight has been gentle — few marks on the clock, more space between arrivals."
            : topBands.Count > 1 && topBands[1].Value > 0
                ? $"Tickets gathered most often during {topBands[0].Key.ToLowerInvariant()}, with {topBands[1].Key.ToLowerInvariant()} holding a softer second pulse."
                : $"The room leaned toward {topBands[0].Key.ToLowerInvariant()} — unhurried, but steady.";

        var openOrders = await db.Orders.AsNoTracking()
            .CountAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled, ct);

        var tablesInPlay = await db.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .Select(o => o.TableCode)
            .Distinct()
            .CountAsync(ct);

        var completionMinutes30 = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.UpdatedAtUtc != null && o.CreatedAtUtc >= since30)
            .Select(o => (o.UpdatedAtUtc!.Value - o.CreatedAtUtc).TotalMinutes)
            .ToListAsync(ct);

        var completionRecent7 = await MedianMinutesAsync(db, since7, now, ct);
        var completionPrev7 = await MedianMinutesAsync(db, prev7Start, prev7End, ct);

        var medianAll = Median(completionMinutes30);

        var sortedMins = completionMinutes30.OrderBy(x => x).ToList();
        var p75 = sortedMins.Count > 0
            ? sortedMins[(int)Math.Clamp(Math.Ceiling(0.75 * sortedMins.Count) - 1, 0, sortedMins.Count - 1)]
            : 0;
        var gentleDelays = p75 > 0 ? completionMinutes30.Count(m => m > p75) : 0;

        var dayMeans = await DayOfWeekMeansAsync(db, since30, ct);
        var smoothestKey = dayMeans.Count > 0 ? dayMeans.OrderBy(kv => kv.Value).First().Key : null;

        var moodRows = await db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since30 && f.MoodKey != null && f.MoodKey != "")
            .GroupBy(f => f.MoodKey!)
            .Select(g => new { Key = g.Key, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .Take(6)
            .ToListAsync(ct);
        var moods = moodRows.Select(x => (x.Key, x.Cnt)).ToList();

        var refinementRows = await db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since30 && f.RefinementKey != null && f.RefinementKey != "")
            .GroupBy(f => f.RefinementKey!)
            .Select(g => new { Key = g.Key, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .Take(6)
            .ToListAsync(ct);
        var refinements = refinementRows.Select(x => (x.Key, x.Cnt)).ToList();

        var outcomeRows = await db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since30)
            .GroupBy(f => f.Outcome)
            .Select(g => new { Key = g.Key, Cnt = g.Count() })
            .ToListAsync(ct);

        var totalFeedback = outcomeRows.Sum(x => x.Cnt);
        var accepted = outcomeRows.Where(x => x.Key.Equals("accepted", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Cnt);
        var ordered = outcomeRows.Where(x => x.Key.Equals("ordered", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Cnt);
        var ignored = outcomeRows.Where(x => x.Key.Equals("ignored", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Cnt);

        var sessionCounts = await db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since30)
            .GroupBy(f => f.SessionId)
            .Select(g => g.Count())
            .ToListAsync(ct);
        var chains = sessionCounts.Count(c => c > 1);

        var fbTimeline = await db.SommelierSuggestionFeedbacks.AsNoTracking()
            .Where(f => f.CreatedAtUtc >= since30)
            .OrderBy(f => f.SessionId)
            .ThenBy(f => f.CreatedAtUtc)
            .Select(f => new FbPair(f.SessionId, f.MenuItemId))
            .ToListAsync(ct);

        var transitions = await BuildTransitionsAsync(db, fbTimeline, ct);

        var orders30Count = await db.Orders.AsNoTracking().CountAsync(o => o.CreatedAtUtc >= since30, ct);
        var ordersWithSignature = await (
            from oi in db.OrderItems.AsNoTracking()
            join o in db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            join mi in db.MenuItems.AsNoTracking() on oi.MenuItemId equals mi.Id
            where o.CreatedAtUtc >= since30 && mi.IsSignature
            select o.Id).Distinct().CountAsync(ct);

        var ordersWithSeasonal = await (
            from oi in db.OrderItems.AsNoTracking()
            join o in db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            join mi in db.MenuItems.AsNoTracking() on oi.MenuItemId equals mi.Id
            where o.CreatedAtUtc >= since30 && mi.IsSignature
            select o.Id).Distinct().CountAsync(ct);

        var topDrinksRaw = await (
            from oi in db.OrderItems.AsNoTracking()
            join o in db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            join mi in db.MenuItems.AsNoTracking() on oi.MenuItemId equals mi.Id
            where o.CreatedAtUtc >= since30
            group oi by new { mi.Id, mi.Name }
            into g
            orderby g.Sum(x => x.Quantity) descending
            select new { g.Key.Name, Cups = g.Sum(x => x.Quantity) })
            .Take(8)
            .ToListAsync(ct);
        var topDrinks = topDrinksRaw.Select(x => (x.Name, x.Cups)).ToList();

        var spark = Enumerable.Range(0, 24)
            .Select(h => new SparkBarVm($"{h:00}", histogram[h] / (double)histMax))
            .ToList();

        var pacingWhisper = IntelligenceEditorial.DescribePacing(completionRecent7, completionPrev7);

        var room = new RoomRhythmSectionVm(
            Narrative: $"{bandNarrative} {pacingWhisper}".Trim(),
            Metrics:
            [
                new QuietMetricVm($"{tablesInPlay} tables in quiet conversation with the floor right now.",
                    openOrders > 0 ? $"{openOrders} open tickets — the line is breathing, not shouting." : "No open tickets — the room is resting between gestures."),
                medianAll is > 0
                    ? new QuietMetricVm($"Median journey from first mark to served: {medianAll:0} minutes.",
                        "A gentle measure of how the room moves, not a stopwatch.")
                    : new QuietMetricVm("Completion times will appear once guests reach the final sip.", null)
            ],
            HourSpark: spark,
            HourHistogramMax: histMax);

        var tasteNarrative = IntelligenceEditorial.ComposeTasteNarrative(moods, refinements, accepted + ordered, ignored);
        var taste = new TasteDirectionSectionVm(
            tasteNarrative,
            moods.Select(x => new NamedCountVm(IntelligenceEditorial.HumanizeKey(x.Key), x.Cnt)).ToList(),
            refinements.Select(x => new NamedCountVm(IntelligenceEditorial.HumanizeKey(x.Key), x.Cnt)).ToList(),
            QuietLines:
            [
                new QuietMetricVm(
                    chains > 0
                        ? $"{chains} sommelier conversations wandered into a second thought — refinements without rush."
                        : "Refinements are still single-breath — guests often trust the first suggestion.",
                    null),
                new QuietMetricVm(
                    totalFeedback > 0
                        ? $"{accepted + ordered} gentle acceptances or orders beside {ignored} quiet declines — listening without pressure."
                        : "Sommelier trails will gather once the room begins whispering back.",
                    null)
            ]);

        var serviceNarrative = IntelligenceEditorial.ComposeServiceNarrative(medianAll, openOrders, gentleDelays, smoothestKey);
        var service = new ServiceAtmosphereSectionVm(
            serviceNarrative,
            Metrics:
            [
                new QuietMetricVm(
                    medianAll is > 0
                        ? $"Average completion arc sits near {medianAll:0} minutes — soft shoulders, not alarms."
                        : "Service rhythm will read more clearly as tickets complete.",
                    null),
                new QuietMetricVm(
                    gentleDelays > 0 && completionMinutes30.Count > 0
                        ? $"{gentleDelays} tickets took a longer breath than most — worth a quiet glance, never a siren."
                        : "No unusual lengthening in the last month — the line stayed kind.",
                    null),
                !string.IsNullOrEmpty(smoothestKey)
                    ? new QuietMetricVm($"{smoothestKey}s felt especially unhurried — handoffs landed with less stretch.", null)
                    : new QuietMetricVm("As more weeks accumulate, a favourite day-of-week calm will emerge here.", null)
            ]);

        var sigPct = orders30Count > 0 ? 100.0 * ordersWithSignature / orders30Count : 0;
        var seaPct = orders30Count > 0 ? 100.0 * ordersWithSeasonal / orders30Count : 0;
        var cupNarrative = IntelligenceEditorial.ComposeCupNarrative(topDrinks, sigPct, seaPct, totalFeedback, ordered);
        var cup = new CupPerformanceSectionVm(
            cupNarrative,
            topDrinks.Select(d => new NamedCountVm(d.Name, d.Cups)).ToList(),
            QuietLines:
            [
                new QuietMetricVm(
                    orders30Count > 0
                        ? $"Signature cups appeared on about {sigPct:0}% of tickets — spotlight momentum in soft numbers."
                        : "No tickets in this window — the line sheet is waiting.",
                    null),
                new QuietMetricVm(
                    orders30Count > 0
                        ? $"Seasonal highlights touched roughly {seaPct:0}% of orders — small seasonal tides."
                        : "Seasonal tides will show once seasonal cups travel the floor.",
                    null)
            ]);

        var sommNarrative = IntelligenceEditorial.ComposeSommelierNarrative(ordered, accepted, ignored, transitions.Count);
        var sommelier = new SommelierSectionVm(
            sommNarrative,
            outcomeRows.OrderByDescending(x => x.Cnt).Select(x => new NamedCountVm(HumanizeOutcome(x.Key), x.Cnt)).ToList(),
            QuietLines:
            [
                new QuietMetricVm(
                    ignored > 0 && totalFeedback > 0
                        ? $"{ignored} suggestions rested unheard — the room chose stillness; that is signal, not failure."
                        : "Few declines on record — curiosity is carrying the room.",
                    null),
                new QuietMetricVm(
                    transitions.Count > 0
                        ? "Strongest sensory handoffs appear below — where one cup suggested the next."
                        : "When guests follow a second pour in one sitting, transitions will surface here.",
                    null)
            ],
            transitions);

        var summaries = IntelligenceEditorial.ComposeSummaries(
            topBands,
            moods,
            refinements,
            medianAll,
            sigPct,
            completionRecent7,
            completionPrev7);

        var lead = summaries.Count > 0
            ? summaries[0]
            : "The observatory is listening — as cups accumulate, this page becomes a quiet diary of the room.";

        return new IntelligencePageVm(
            HeroEyebrow: "Kits Coffee Atelier",
            HeroTitle: "Hospitality observatory",
            HeroLead: lead,
            EditorialSummaries: summaries.Skip(1).Take(4).ToList(),
            RoomRhythm: room,
            TasteDirection: taste,
            ServiceAtmosphere: service,
            CupPerformance: cup,
            Sommelier: sommelier);
    }

    private static string HumanizeOutcome(string o) =>
        o.ToLowerInvariant() switch
        {
            "ordered" => "Ordered",
            "accepted" => "Accepted",
            "ignored" => "Quietly set aside",
            _ => o
        };

    private static async Task<double?> MedianMinutesAsync(IApplicationDbContext db, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var list = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.UpdatedAtUtc != null
                && o.CreatedAtUtc >= from && o.CreatedAtUtc < to)
            .Select(o => (o.UpdatedAtUtc!.Value - o.CreatedAtUtc).TotalMinutes)
            .ToListAsync(ct);
        return Median(list);
    }

    private static double? Median(List<double> values)
    {
        if (values.Count == 0) return null;
        values.Sort();
        return values[values.Count / 2];
    }

    private static async Task<Dictionary<string, double>> DayOfWeekMeansAsync(IApplicationDbContext db, DateTimeOffset since30, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.UpdatedAtUtc != null && o.CreatedAtUtc >= since30)
            .Select(o => new { o.CreatedAtUtc, Mins = (o.UpdatedAtUtc!.Value - o.CreatedAtUtc).TotalMinutes })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.CreatedAtUtc.ToLocalTime().DayOfWeek)
            .ToDictionary(g => g.Key.ToString(), g => g.Average(x => x.Mins));
    }

    private static async Task<IReadOnlyList<TransitionVm>> BuildTransitionsAsync(
        IApplicationDbContext db,
        IReadOnlyList<FbPair> timeline,
        CancellationToken ct)
    {
        if (timeline.Count < 2)
            return [];

        var pairs = new List<(Guid From, Guid To)>();
        Guid? lastSession = null;
        Guid? lastMenu = null;
        foreach (var row in timeline)
        {
            if (lastSession == row.SessionId && lastMenu is { } lm && lm != row.MenuItemId)
                pairs.Add((lm, row.MenuItemId));
            lastSession = row.SessionId;
            lastMenu = row.MenuItemId;
        }

        if (pairs.Count == 0)
            return [];

        var top = pairs
            .GroupBy(p => (p.From, p.To))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => (g.Key.From, g.Key.To, Times: g.Count()))
            .ToList();

        var ids = top.SelectMany(t => new[] { t.From, t.To }).Distinct().ToList();
        var names = await db.MenuItems.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return top.Select(t =>
        {
            names.TryGetValue(t.From, out var fn);
            names.TryGetValue(t.To, out var tn);
            return new TransitionVm(
                string.IsNullOrEmpty(fn) ? "Earlier pour" : fn,
                string.IsNullOrEmpty(tn) ? "Next pour" : tn,
                t.Times);
        }).ToList();
    }
}
