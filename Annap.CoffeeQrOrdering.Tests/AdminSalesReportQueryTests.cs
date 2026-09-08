using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class AdminSalesReportQueryTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private static DateTime TestDay(int offset) => new DateTime(2031, 1, 1).AddDays(offset);

    [Fact]
    public async Task Submitted_unpaid_orders_are_excluded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(1);

        await SalesReportTestSeedHelper.InsertSubmittedOrderAsync(
            db, ctx, DateTimeOffset.UtcNow, ctx.MenuItemAId, "Coco Bơ", 55000m, 2);

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(0, report.PaidOrderCount);
        Assert.Equal(0m, report.TotalRevenue);
    }

    [Fact]
    public async Task Paid_orders_are_included()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(2);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(1, report.PaidOrderCount);
        Assert.Equal(110000m, report.TotalRevenue);
        Assert.Equal(2, report.TotalQuantity);
    }

    [Fact]
    public async Task Completed_paid_orders_are_included()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(3);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Completed,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(1, report.PaidOrderCount);
        Assert.Equal(55000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Revenue_uses_order_item_unit_price_snapshot()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(4);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 48000m, 3));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(144000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Changing_current_menu_price_does_not_change_report_revenue()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(5);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        var menu = await db.MenuItems.SingleAsync(m => m.Id == ctx.MenuItemAId);
        menu.Price = 99000m;
        await db.SaveChangesAsync();

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(110000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Date_range_filters_by_paid_at_utc_not_created_at_utc()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var today = TestDay(100);
        var yesterday = today.AddDays(-1);

        var paidAtToday = SalesReportTestSeedHelper.PaidAtForLocalDate(today);
        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAtToday, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        order.CreatedAtUtc = SalesReportTestSeedHelper.PaidAtForLocalDate(yesterday).AddDays(-1);
        await db.SaveChangesAsync();

        var todayReport = await AdminSalesReportQuery.LoadAsync(db, today, today);
        var yesterdayReport = await AdminSalesReportQuery.LoadAsync(db, yesterday, yesterday);

        Assert.Equal(1, todayReport.PaidOrderCount);
        Assert.Equal(0, yesterdayReport.PaidOrderCount);
    }

    [Fact]
    public async Task Today_filter_uses_vietnam_local_boundaries()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var today = TestDay(300);
        var (utcStart, _) = AnnapBusinessTime.ToUtcRangeInclusive(today, today);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, utcStart.AddMinutes(30), OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, utcStart.AddMinutes(-30), OrderStatus.Paid,
            (ctx.MenuItemBId, "Hibiscus Tea", 45000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, today, today);

        Assert.Equal(1, report.PaidOrderCount);
        Assert.Equal(55000m, report.TotalRevenue);
    }

    [Fact]
    public async Task Product_aggregation_sums_quantity_correctly()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(8);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt.AddMinutes(5), OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 3));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);
        var product = Assert.Single(report.ProductSales);

        Assert.Equal(5, product.Quantity);
        Assert.Equal(2, product.OrderCount);
        Assert.Equal(275000m, product.Revenue);
    }

    [Fact]
    public async Task Best_seller_is_highest_quantity()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(9);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 9),
            (ctx.MenuItemBId, "Hibiscus Tea", 45000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.NotNull(report.BestSeller);
        Assert.Equal("Coco Bơ", report.BestSeller!.Name);
        Assert.Equal(9, report.BestSeller.Quantity);
    }

    [Fact]
    public async Task Slow_seller_is_lowest_quantity_among_sold_items()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(10);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 9),
            (ctx.MenuItemBId, "Hibiscus Tea", 45000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.NotNull(report.SlowSeller);
        Assert.Equal("Hibiscus Tea", report.SlowSeller!.Name);
        Assert.Equal(1, report.SlowSeller.Quantity);
    }

    [Fact]
    public async Task Empty_range_returns_zero_without_crash()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var future = TestDay(400);

        var report = await AdminSalesReportQuery.LoadAsync(db, future, future);

        Assert.Equal(0, report.PaidOrderCount);
        Assert.Equal(0m, report.TotalRevenue);
        Assert.Empty(report.ProductSales);
        Assert.Contains("Chưa có thanh toán", report.ReportText);
        Assert.Single(report.DailySummary);
        Assert.Equal(0m, report.DailySummary[0].Revenue);
    }

    [Fact]
    public async Task Empty_three_day_range_returns_three_zero_daily_rows()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var from = TestDay(410);
        var to = from.AddDays(2);

        var report = await AdminSalesReportQuery.LoadAsync(db, from, to);

        Assert.Equal(3, report.DailySummary.Count);
        Assert.All(report.DailySummary, d =>
        {
            Assert.Equal(0m, d.Revenue);
            Assert.Equal(0, d.PaidOrderCount);
            Assert.Equal(0, d.Quantity);
            Assert.Null(d.TopProductName);
        });
        Assert.False(report.DailySummaryLimited);
    }

    [Fact]
    public async Task Large_range_daily_summary_is_limited()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var from = TestDay(420);
        var to = from.AddDays(AdminSalesReportQuery.MaxDailySummaryDays);

        var report = await AdminSalesReportQuery.LoadAsync(db, from, to);

        Assert.True(report.DailySummaryLimited);
        Assert.Equal(AdminSalesReportQuery.DailySummaryLimitMessage, report.DailySummaryNotice);
        Assert.Empty(report.DailySummary);
    }

    [Fact]
    public async Task Product_quantity_share_percent_is_calculated_correctly()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(12);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 10),
            (ctx.MenuItemBId, "Hibiscus Tea", 45000m, 5));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);
        var top = report.ProductSales.Single(p => p.Name == "Coco Bơ");
        var slow = report.ProductSales.Single(p => p.Name == "Hibiscus Tea");

        Assert.Equal(10, top.Quantity);
        Assert.Equal(66.7m, Math.Round(top.QuantitySharePercent, 1));
        Assert.Equal(5, slow.Quantity);
        Assert.Equal(33.3m, Math.Round(slow.QuantitySharePercent, 1));
        Assert.Equal(100m, top.QuantitySharePercent + slow.QuantitySharePercent);
    }

    [Fact]
    public async Task Cancelled_orders_are_excluded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(13);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Cancelled,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(0, report.PaidOrderCount);
        Assert.Equal(0m, report.TotalRevenue);
    }

    [Fact]
    public async Task Draft_orders_are_excluded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(14);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertOrderAsync(db, ctx, new Order
        {
            Status = OrderStatus.Draft,
            PaidAtUtc = paidAt,
            StatusChangedAtUtc = paidAt,
            Items =
            [
                new OrderItem
                {
                    MenuItemId = ctx.MenuItemAId,
                    MenuItemName = "Coco Bơ",
                    UnitPrice = 55000m,
                    Quantity = 1
                }
            ]
        });

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(0, report.PaidOrderCount);
    }

    [Fact]
    public async Task Null_menu_item_name_uses_fallback_label()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(15);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(day);

        await SalesReportTestSeedHelper.InsertOrderAsync(db, ctx, new Order
        {
            Status = OrderStatus.Paid,
            PaidAtUtc = paidAt,
            StatusChangedAtUtc = paidAt,
            Items =
            [
                new OrderItem
                {
                    MenuItemId = ctx.MenuItemAId,
                    MenuItemName = null,
                    UnitPrice = 55000m,
                    Quantity = 1
                }
            ]
        });

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);
        var product = Assert.Single(report.ProductSales);

        Assert.Equal("Món không xác định", product.Name);
    }

    [Fact]
    public async Task Legacy_completed_without_paid_at_is_excluded()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var day = TestDay(16);

        await SalesReportTestSeedHelper.InsertOrderAsync(db, ctx, new Order
        {
            Status = OrderStatus.Completed,
            PaidAtUtc = null,
            CompletedAtUtc = SalesReportTestSeedHelper.PaidAtForLocalDate(day),
            StatusChangedAtUtc = SalesReportTestSeedHelper.PaidAtForLocalDate(day),
            Items =
            [
                new OrderItem
                {
                    MenuItemId = ctx.MenuItemAId,
                    MenuItemName = "Coco Bơ",
                    UnitPrice = 55000m,
                    Quantity = 2
                }
            ]
        });

        var report = await AdminSalesReportQuery.LoadAsync(db, day, day);

        Assert.Equal(0, report.PaidOrderCount);
        Assert.Equal(0m, report.TotalRevenue);
    }

    [Fact]
    public async Task Daily_summary_groups_by_local_date()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var today = TestDay(200);
        var yesterday = today.AddDays(-1);

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, SalesReportTestSeedHelper.PaidAtForLocalDate(today), OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, SalesReportTestSeedHelper.PaidAtForLocalDate(yesterday), OrderStatus.Paid,
            (ctx.MenuItemBId, "Hibiscus Tea", 45000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, yesterday, today);

        Assert.Equal(2, report.DailySummary.Count);
        Assert.False(report.DailySummaryLimited);
        var dayYesterday = report.DailySummary.Single(d => d.LocalDate == yesterday);
        var dayToday = report.DailySummary.Single(d => d.LocalDate == today);

        Assert.Equal(45000m, dayYesterday.Revenue);
        Assert.Equal(1, dayYesterday.PaidOrderCount);
        Assert.Equal("Hibiscus Tea", dayYesterday.TopProductName);

        Assert.Equal(110000m, dayToday.Revenue);
        Assert.Equal(1, dayToday.PaidOrderCount);
        Assert.Equal("Coco Bơ", dayToday.TopProductName);
    }
}
