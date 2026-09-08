using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

internal static class OrderTestSeedHelper
{
    internal sealed record OrderSubmitFixture(Guid VenueTableId, Guid MenuItemId, decimal MenuPrice, string MenuItemName);

    internal static async Task<OrderSubmitFixture> SeedMinimalOrderSubmitDataAsync(AppDbContext db)
    {
        var category = new MenuCategory
        {
            Name = "Integration Test Coffees",
            SortOrder = 1
        };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        var menuItem = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Test Latte",
            Price = 65000m,
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(menuItem);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = $"T{suffix[..2]}{suffix[2..4]}",
            PublicSlug = $"annap-t-{suffix}",
            DisplayLabel = $"Test Table {suffix[..4]}",
            IsActive = true
        };
        db.VenueTables.Add(table);

        await db.SaveChangesAsync();

        return new OrderSubmitFixture(table.Id, menuItem.Id, menuItem.Price, menuItem.Name);
    }

    internal static async Task<int> CountOrdersAsync(AppDbContext db) =>
        await db.Orders.CountAsync();

    internal static async Task<decimal?> GetOrderLineUnitPriceAsync(AppDbContext db, Guid orderId) =>
        await db.OrderItems
            .Where(i => i.OrderId == orderId)
            .Select(i => (decimal?)i.UnitPrice)
            .FirstOrDefaultAsync();
}
