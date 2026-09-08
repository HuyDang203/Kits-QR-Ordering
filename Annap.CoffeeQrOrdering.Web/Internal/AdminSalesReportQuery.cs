using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Internal;

public sealed record AdminProductSalesRowVm(
    Guid MenuItemId,
    string Name,
    int Quantity,
    int OrderCount,
    decimal Revenue,
    decimal AverageUnitPrice,
    decimal QuantitySharePercent);

public sealed record AdminPaidBillRowVm(
    Guid OrderId,
    string BillNumber,
    string TableCode,
    DateTimeOffset PaidAtUtc,
    int ItemQuantity,
    decimal TotalAmount,
    string StatusLabel);

public sealed record AdminDailySummaryRowVm(
    DateTime LocalDate,
    decimal Revenue,
    int PaidOrderCount,
    int Quantity,
    string? TopProductName);

public sealed record AdminSalesReportVm(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    DateTimeOffset UtcStart,
    DateTimeOffset UtcEndExclusive,
    decimal TotalRevenue,
    int PaidOrderCount,
    int TotalQuantity,
    decimal AverageOrderValue,
    IReadOnlyList<AdminProductSalesRowVm> ProductSales,
    AdminProductSalesRowVm? BestSeller,
    AdminProductSalesRowVm? SlowSeller,
    IReadOnlyList<AdminPaidBillRowVm> PaidBills,
    IReadOnlyList<AdminDailySummaryRowVm> DailySummary,
    bool DailySummaryLimited,
    string? DailySummaryNotice,
    string ReportText);

internal static class AdminSalesReportQuery
{
    public const int MaxDailySummaryDays = 62;

    public const string DailySummaryLimitMessage =
        "Bảng theo ngày chỉ hiển thị tối đa 62 ngày. Hãy thu hẹp khoảng thời gian để xem chi tiết từng ngày.";
    private static readonly OrderStatus[] RevenueStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.InProgress,
        OrderStatus.FinishingTouches,
        OrderStatus.Ready,
        OrderStatus.Completed
    ];

    public static async Task<AdminSalesReportVm> LoadAsync(
        IApplicationDbContext db,
        DateTime fromLocalInclusive,
        DateTime toLocalInclusive,
        CancellationToken ct = default)
    {
        var from = fromLocalInclusive.Date;
        var to = toLocalInclusive.Date;
        if (from > to)
            (from, to) = (to, from);

        var (utcStart, utcEndExclusive) = AnnapBusinessTime.ToUtcRangeInclusive(from, to);

        var itemRows = await (
            from oi in db.OrderItems.AsNoTracking()
            join o in db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            where o.PaidAtUtc != null
                  && o.PaidAtUtc >= utcStart
                  && o.PaidAtUtc < utcEndExclusive
                  && RevenueStatuses.Contains(o.Status)
            select new ItemProjection(
                oi.OrderId,
                oi.MenuItemId,
                oi.MenuItemName,
                oi.Quantity,
                oi.UnitPrice,
                o.PaidAtUtc!.Value))
            .ToListAsync(ct);

        var paidOrderCount = itemRows.Select(x => x.OrderId).Distinct().Count();
        var totalRevenue = itemRows.Sum(x => x.UnitPrice * x.Quantity);
        var totalQuantity = itemRows.Sum(x => x.Quantity);
        var averageOrderValue = paidOrderCount > 0 ? totalRevenue / paidOrderCount : 0m;

        var productSales = BuildProductSales(itemRows, totalQuantity);
        var bestSeller = productSales.Count > 0
            ? productSales.OrderByDescending(p => p.Quantity).ThenBy(p => p.Name).First()
            : null;
        var slowSeller = productSales.Count > 0
            ? productSales.OrderBy(p => p.Quantity).ThenBy(p => p.Name).First()
            : null;

        var paidBills = await LoadPaidBillsAsync(db, utcStart, utcEndExclusive, ct);
        var (dailySummary, dailyLimited, dailyNotice) = BuildDailySummary(itemRows, from, to);
        var reportText = BuildReportText(from, to, totalRevenue, paidOrderCount, totalQuantity, bestSeller, slowSeller);

        return new AdminSalesReportVm(
            from,
            to,
            utcStart,
            utcEndExclusive,
            totalRevenue,
            paidOrderCount,
            totalQuantity,
            averageOrderValue,
            productSales,
            bestSeller,
            slowSeller,
            paidBills,
            dailySummary,
            dailyLimited,
            dailyNotice,
            reportText);
    }

    private static List<AdminProductSalesRowVm> BuildProductSales(
        IReadOnlyList<ItemProjection> itemRows,
        int totalQuantity)
    {
        if (itemRows.Count == 0)
            return [];

        return itemRows
            .GroupBy(i => i.MenuItemId)
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                var revenue = g.Sum(x => x.UnitPrice * x.Quantity);
                var orderCount = g.Select(x => x.OrderId).Distinct().Count();
                var share = totalQuantity > 0 ? 100m * qty / totalQuantity : 0m;
                var name = g.Select(x => NormalizeName(x.MenuItemName)).First();
                return new AdminProductSalesRowVm(
                    g.Key,
                    name,
                    qty,
                    orderCount,
                    revenue,
                    qty > 0 ? revenue / qty : 0m,
                    share);
            })
            .OrderByDescending(p => p.Quantity)
            .ThenBy(p => p.Name)
            .ToList();
    }

    private static async Task<List<AdminPaidBillRowVm>> LoadPaidBillsAsync(
        IApplicationDbContext db,
        DateTimeOffset utcStart,
        DateTimeOffset utcEndExclusive,
        CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.PaidAtUtc != null
                        && o.PaidAtUtc >= utcStart
                        && o.PaidAtUtc < utcEndExclusive
                        && RevenueStatuses.Contains(o.Status))
            .OrderByDescending(o => o.PaidAtUtc)
            .Select(o => new
            {
                o.Id,
                o.BillNumber,
                o.TableCode,
                PaidAtUtc = o.PaidAtUtc!.Value,
                o.Status,
                o.TotalAmount,
                ItemQuantity = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(ct);

        return rows.Select(o => new AdminPaidBillRowVm(
            o.Id,
            FormatBillNumber(o.Id, o.BillNumber),
            o.TableCode,
            o.PaidAtUtc,
            o.ItemQuantity,
            o.TotalAmount,
            ResolveStatusLabel(o.Status))).ToList();
    }

    private static (List<AdminDailySummaryRowVm> Rows, bool Limited, string? Notice) BuildDailySummary(
        IReadOnlyList<ItemProjection> itemRows,
        DateTime from,
        DateTime to)
    {
        var dayCount = (to - from).Days + 1;
        if (dayCount > MaxDailySummaryDays)
            return ([], true, DailySummaryLimitMessage);

        var byDay = itemRows
            .GroupBy(i => AnnapBusinessTime.ToLocalDate(i.PaidAtUtc))
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = new List<AdminDailySummaryRowVm>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!byDay.TryGetValue(d, out var dayItems))
            {
                days.Add(new AdminDailySummaryRowVm(d, 0m, 0, 0, null));
                continue;
            }

            var revenue = dayItems.Sum(x => x.UnitPrice * x.Quantity);
            var orderCount = dayItems.Select(x => x.OrderId).Distinct().Count();
            var quantity = dayItems.Sum(x => x.Quantity);
            var top = dayItems
                .GroupBy(x => NormalizeName(x.MenuItemName))
                .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Qty)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            days.Add(new AdminDailySummaryRowVm(
                d,
                revenue,
                orderCount,
                quantity,
                top?.Name));
        }

        return (days, false, null);
    }

    private static string BuildReportText(
        DateTime from,
        DateTime to,
        decimal totalRevenue,
        int paidOrderCount,
        int totalQuantity,
        AdminProductSalesRowVm? bestSeller,
        AdminProductSalesRowVm? slowSeller)
    {
        if (paidOrderCount == 0)
            return "Chưa có thanh toán nào trong khoảng thời gian này.";

        var revenueText = VndMoneyFormatter.Format(totalRevenue);
        var rangeLabel = from == to
            ? $"Báo cáo ngày {AnnapBusinessTime.FormatLocalDateLong(from)}:"
            : $"Báo cáo từ {AnnapBusinessTime.FormatLocalDateLong(from)} đến {AnnapBusinessTime.FormatLocalDateLong(to)}:";

        var body = from == to
            ? $"Tổng doanh thu đạt {revenueText} từ {paidOrderCount} đơn đã thanh toán, với {totalQuantity} ly được bán ra."
            : $"Tổng doanh thu đạt {revenueText} từ {paidOrderCount} đơn đã thanh toán. Tổng số ly bán ra là {totalQuantity}.";

        if (bestSeller is not null)
            body += $" Món bán chạy nhất là {bestSeller.Name} với {bestSeller.Quantity} ly.";

        if (slowSeller is not null && productCountDistinct(bestSeller, slowSeller))
            body += $" Món bán chậm nhất trong nhóm có phát sinh doanh số là {slowSeller.Name} với {slowSeller.Quantity} ly.";

        return $"{rangeLabel}\n{body}";

        static bool productCountDistinct(AdminProductSalesRowVm? best, AdminProductSalesRowVm slow) =>
            best is null || best.MenuItemId != slow.MenuItemId || best.Quantity != slow.Quantity;
    }

    internal static bool IsRevenueOrder(Order order) =>
        order.PaidAtUtc != null && RevenueStatuses.Contains(order.Status);

    internal static bool IsOrderInReportRange(
        Order order,
        DateTimeOffset utcStart,
        DateTimeOffset utcEndExclusive) =>
        order.PaidAtUtc is { } paid
        && paid >= utcStart
        && paid < utcEndExclusive
        && IsRevenueOrder(order);

    internal static string NormalizeName(string? menuItemName) =>
        string.IsNullOrWhiteSpace(menuItemName) ? "Món không xác định" : menuItemName.Trim();

    private static string FormatBillNumber(Guid orderId, string? billNumber)
    {
        if (!string.IsNullOrWhiteSpace(billNumber))
            return billNumber.Trim();
        return $"A{orderId.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string ResolveStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Completed => "Đã thanh toán",
        OrderStatus.Paid or OrderStatus.InProgress or OrderStatus.FinishingTouches or OrderStatus.Ready => "Đã thanh toán",
        _ => "—"
    };

    private sealed record ItemProjection(
        Guid OrderId,
        Guid MenuItemId,
        string? MenuItemName,
        int Quantity,
        decimal UnitPrice,
        DateTimeOffset PaidAtUtc);
}
