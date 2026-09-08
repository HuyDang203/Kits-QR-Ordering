using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

internal static class SalesReportTestSeedHelper
{
    internal sealed record SalesSeedContext(
        Guid VenueTableId,
        Guid MenuItemAId,
        Guid MenuItemBId,
        string TableCode);

    internal static async Task<SalesSeedContext> SeedMenuAndTableAsync(AppDbContext db)
    {
        var category = new MenuCategory { Name = "Report Test", SortOrder = 99 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        var itemA = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Coco Bơ",
            Price = 55000m,
            IsAvailable = true
        };
        var itemB = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Hibiscus Tea",
            Price = 45000m,
            IsAvailable = true
        };
        db.MenuItems.AddRange(itemA, itemB);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = $"R{suffix[..3]}",
            PublicSlug = $"annap-r-{suffix}",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();

        return new SalesSeedContext(table.Id, itemA.Id, itemB.Id, table.DisplayCode);
    }

    internal static async Task<Guid> InsertPaidOrderAsync(
        AppDbContext db,
        SalesSeedContext ctx,
        DateTimeOffset paidAtUtc,
        OrderStatus status,
        params (Guid MenuItemId, string Name, decimal UnitPrice, int Quantity)[] lines)
    {
        var order = new Order
        {
            VenueTableId = ctx.VenueTableId,
            TableCode = ctx.TableCode,
            Status = status,
            PaidAtUtc = paidAtUtc,
            StatusChangedAtUtc = paidAtUtc,
            BillNumber = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            Items = lines.Select(l => new OrderItem
            {
                MenuItemId = l.MenuItemId,
                MenuItemName = l.Name,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity
            }).ToList()
        };
        order.RecalculateTotals();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    internal static async Task<Guid> InsertOrderAsync(
        AppDbContext db,
        SalesSeedContext ctx,
        Order order)
    {
        order.VenueTableId = ctx.VenueTableId;
        order.TableCode = ctx.TableCode;
        order.RecalculateTotals();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    internal static async Task<Guid> InsertSubmittedOrderAsync(
        AppDbContext db,
        SalesSeedContext ctx,
        DateTimeOffset createdAtUtc,
        Guid menuItemId,
        string name,
        decimal unitPrice,
        int quantity)
    {
        var order = new Order
        {
            VenueTableId = ctx.VenueTableId,
            TableCode = ctx.TableCode,
            Status = OrderStatus.Submitted,
            CreatedAtUtc = createdAtUtc,
            StatusChangedAtUtc = createdAtUtc,
            Items =
            [
                new OrderItem
                {
                    MenuItemId = menuItemId,
                    MenuItemName = name,
                    UnitPrice = unitPrice,
                    Quantity = quantity
                }
            ]
        };
        order.RecalculateTotals();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    internal static (DateTime From, DateTime To) TodayLocalRange() =>
        (AnnapBusinessTime.TodayLocal, AnnapBusinessTime.TodayLocal);

    internal static DateTimeOffset PaidAtForLocalDate(DateTime localDate, int hourLocal = 12) =>
        AnnapBusinessTime.ToUtcRangeInclusive(localDate, localDate).UtcStart.AddHours(hourLocal);
}
