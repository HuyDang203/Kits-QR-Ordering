using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Tables;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public IReadOnlyList<FloorTableRowVm> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var tables = await db.VenueTables.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayCode)
            .ToListAsync(cancellationToken);

        var openOrders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.VenueTableId != null
                && o.Status != OrderStatus.Completed
                && o.Status != OrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var byTable = openOrders
            .GroupBy(o => o.VenueTableId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var lastRows = await db.Orders.AsNoTracking()
            .Where(o => o.VenueTableId != null)
            .GroupBy(o => o.VenueTableId!.Value)
            .Select(g => new { Id = g.Key, Last = g.Max(x => x.UpdatedAtUtc ?? x.CreatedAtUtc) })
            .ToListAsync(cancellationToken);
        var lastByTable = lastRows.ToDictionary(x => x.Id, x => x.Last);

        var rows = new List<FloorTableRowVm>();
        foreach (var t in tables)
        {
            var orders = byTable.GetValueOrDefault(t.Id, []);
            var openSpend = orders.Sum(o => o.TotalAmount);
            var cups = orders.Sum(o => o.Items.Sum(i => i.Quantity));
            lastByTable.TryGetValue(t.Id, out var lastAct);
            var oldest = orders.Count == 0 ? null : orders.MinBy(o => o.CreatedAtUtc);
            var pacing = ResolvePacing(oldest);
            var state = orders.Count == 0 ? "free" : "active";
            rows.Add(new FloorTableRowVm(
                t.Id,
                t.DisplayCode,
                t.DisplayLabel,
                orders.Count,
                cups,
                openSpend,
                lastAct,
                pacing,
                state));
        }

        Rows = rows;
    }

    private static string ResolvePacing(Order? oldestOpen)
    {
        if (oldestOpen is null)
            return "steady";
        var mins = (DateTimeOffset.UtcNow - oldestOpen.CreatedAtUtc).TotalMinutes;
        if (mins > 18) return "watch";
        if (mins > 10) return "steady";
        return "steady";
    }
}

public sealed record FloorTableRowVm(
    Guid Id,
    string DisplayCode,
    string? DisplayLabel,
    int OpenOrders,
    int OpenCups,
    decimal OpenSpend,
    DateTimeOffset? LastActivityUtc,
    string PacingKey,
    string FloorState);
