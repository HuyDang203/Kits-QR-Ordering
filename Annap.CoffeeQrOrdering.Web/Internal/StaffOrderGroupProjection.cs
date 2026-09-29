using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// POST /api/staff/order-groups body. Two or more distinct, currently
/// board-eligible OrderIds. Server dedupes, validates, and stores.
/// </summary>
internal sealed record StaffOrderGroupRequest(List<Guid>? OrderIds);

/// <summary>
/// POST /api/staff/order-groups/{id}/remove body.
/// </summary>
internal sealed record StaffOrderGroupMemberRequest(Guid OrderId);

/// <summary>
/// Read-only projection layer for staff Column 3 grouping. The aggregate
/// values (total, tableCodes, item snapshot) are derived from the underlying
/// OrderItems at read time — never recomputed against MenuItem.Price — so the
/// grouped card always matches the sum of the original ticket snapshots.
/// </summary>
internal static class StaffOrderGroupProjectionHelper
{
    public static async Task<List<object>> BuildBoardGroupsAsync(AppDbContext db, CancellationToken ct)
    {
        var groups = await db.StaffOrderGroups
            .AsNoTracking()
            .Include(g => g.Members)
                .ThenInclude(m => m.Order)
                    .ThenInclude(o => o.Items)
            .OrderBy(g => g.CreatedAtUtc)
            .ToListAsync(ct);

        var result = new List<object>(groups.Count);
        foreach (var g in groups)
        {
            result.Add(Project(g));
        }
        return result;
    }

    public static async Task<object?> LoadGroupAsync(AppDbContext db, Guid groupId, CancellationToken ct)
    {
        var g = await db.StaffOrderGroups
            .AsNoTracking()
            .Include(grp => grp.Members)
                .ThenInclude(m => m.Order)
                    .ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(grp => grp.Id == groupId, ct);
        return g is null ? null : Project(g);
    }

    private static object Project(StaffOrderGroup g)
    {
        var orders = g.Members
            .Select(m => m.Order)
            .Where(o => o != null)
            .OrderBy(o => o.CreatedAtUtc)
            .ToList();
        var primary = orders.FirstOrDefault();

        // Aggregate total — exactly the sum of OrderItem snapshots. The original
        // Order rows are never mutated by grouping; the total is purely a
        // presentation-layer summation.
        var aggregateTotal = orders.Sum(o => o.Items.Sum(i => i.UnitPrice * i.Quantity));

        var tableCodes = orders
            .Select(o => string.IsNullOrWhiteSpace(o.TableCode) ? "—" : o.TableCode)
            .Distinct()
            .ToList();

        return new
        {
            g.Id,
            g.CreatedByStaffName,
            g.CreatedByStaffAccountId,
            g.CreatedAtUtc,
            g.UpdatedAtUtc,
            orderIds = orders.Select(o => o.Id).ToList(),
            primaryOrderId = primary?.Id,
            tableCodes,
            tableCodeDisplay = string.Join(", ", tableCodes),
            memberCount = orders.Count,
            aggregateTotal,
            // Helpful for the client to know the column where the primary
            // member currently sits; the grouped card renders there.
            primaryBoardColumn = primary is null
                ? null
                : StaffOrderBoardColumnHelper.ToColumn(primary.Status),
            primaryStaffStatus = primary is null
                ? null
                : StaffOrderStatusHelper.ToStaffStatus(primary.Status)
        };
    }
}