using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Integration tests for the Staff Column 3 order-grouping feature.
/// The grouping layer is strictly a presentation concern — the underlying
/// Orders and OrderItems are never modified. See:
///   POST   /api/staff/order-groups
///   POST   /api/staff/order-groups/{groupId}/remove
///   DELETE /api/staff/order-groups/{groupId}
///
/// Test cases (from the spec):
///   A — same table, 30k + 30k → 60k
///   B — different tables, 36k + 40k → 76k
///   C — multiple same-table Orders
///   D — extend an existing group
///   E — duplicate OrderId is idempotent
///   F — pricing uses OrderItem snapshot
///   G — non-board-eligible Order is rejected
///   H — refresh persistence
///   I — ungroup dissolves the group and preserves Orders
/// </summary>
public sealed class StaffOrderGroupingTests : IClassFixture<AnnapPostgresWebApplicationFactory>, IAsyncLifetime
{
    private readonly AnnapPostgresWebApplicationFactory _factory;
    private HttpClient _staffClient = null!;

    public StaffOrderGroupingTests(AnnapPostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Single login shared across all tests in the class — the staff-login
        // rate-limit policy allows only 8 POSTs / 5 min per partition. Use a
        // unique X-Forwarded-For header so this class gets its own bucket even
        // when other test classes share the same host.
        _staffClient = _factory.CreateClient();
        _staffClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Forwarded-For",
            $"10.99.99.{Random.Shared.Next(1, 250)}");
        await LoginStaffAsync(_staffClient);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CaseA_same_table_two_orders_group_to_60k()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (menuItem, _) = await SeedMenuItemAsync(db, "Cafe đen", 30000m);
        var tableA = await SeedTableAsync(db, "T36");

        var orderA = await SeedSubmittedOrderAsync(db, tableA, menuItem, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, tableA, menuItem, quantity: 1);

        var groupRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, groupRes.statusCode);
        var group = groupRes.body;

        Assert.Equal(2, group.GetProperty("memberCount").GetInt32());
        Assert.Equal(60000m, group.GetProperty("aggregateTotal").GetDecimal());

        var tableCodes = group.GetProperty("tableCodes").EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => s != null)
            .Cast<string>()
            .ToList();
        Assert.Single(tableCodes);
        // Display code is suffixed with a per-test unique id; the prefix is what
        // matters semantically.
        Assert.StartsWith("T36", tableCodes[0]);

        // Original Orders and OrderItems remain unchanged.
        Assert.Equal(30000m, await db.Orders.Where(o => o.Id == orderA).Select(o => o.TotalAmount).FirstAsync());
        Assert.Equal(30000m, await db.Orders.Where(o => o.Id == orderB).Select(o => o.TotalAmount).FirstAsync());
    }

    [Fact]
    public async Task CaseB_different_tables_group_to_76k_with_joined_tablecodes()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Cafe đen", 36000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Trà vải", 40000m);
        var table36 = await SeedTableAsync(db, "T36");
        var table37 = await SeedTableAsync(db, "T37");

        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table37, item2, quantity: 1);

        var groupRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, groupRes.statusCode);
        var group = groupRes.body;

        Assert.Equal(2, group.GetProperty("memberCount").GetInt32());
        Assert.Equal(76000m, group.GetProperty("aggregateTotal").GetDecimal());

        var tableCodes = group.GetProperty("tableCodes").EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => s != null)
            .Cast<string>()
            .ToList();
        Assert.Equal(2, tableCodes.Count);
        Assert.Contains(tableCodes, s => s.StartsWith("T36"));
        Assert.Contains(tableCodes, s => s.StartsWith("T37"));
    }

    [Fact]
    public async Task CaseC_multiple_same_table_orders_all_in_one_group()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Item 2", 20000m);
        var (item3, _) = await SeedMenuItemAsync(db, "Item 3", 30000m);
        var table36 = await SeedTableAsync(db, "T36");

        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item2, quantity: 1);
        var orderC = await SeedSubmittedOrderAsync(db, table36, item3, quantity: 1);

        var groupRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB, orderC });
        Assert.Equal(HttpStatusCode.OK, groupRes.statusCode);
        var group = groupRes.body;

        Assert.Equal(3, group.GetProperty("memberCount").GetInt32());
        Assert.Equal(60000m, group.GetProperty("aggregateTotal").GetDecimal());

        var memberIds = group.GetProperty("orderIds").EnumerateArray()
            .Select(e => e.GetGuid())
            .ToList();
        Assert.Contains(orderA, memberIds);
        Assert.Contains(orderB, memberIds);
        Assert.Contains(orderC, memberIds);
    }

    [Fact]
    public async Task CaseD_extending_existing_group_adds_new_order()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Item 2", 20000m);
        var (item3, _) = await SeedMenuItemAsync(db, "Item 3", 30000m);
        var table36 = await SeedTableAsync(db, "T36");

        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item2, quantity: 1);
        var orderC = await SeedSubmittedOrderAsync(db, table36, item3, quantity: 1);

        // Step 1: [A, B]
        var firstRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, firstRes.statusCode);
        var firstGroupId = firstRes.body.GetProperty("id").GetGuid();
        Assert.Equal(2, firstRes.body.GetProperty("memberCount").GetInt32());

        // Step 2: extend to [A, B, C] via a fresh POST
        var secondRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB, orderC });
        Assert.Equal(HttpStatusCode.OK, secondRes.statusCode);
        var secondGroupId = secondRes.body.GetProperty("id").GetGuid();
        Assert.Equal(3, secondRes.body.GetProperty("memberCount").GetInt32());
        Assert.Equal(60000m, secondRes.body.GetProperty("aggregateTotal").GetDecimal());

        // No nested groups — there is exactly one group holding all three orders.
        var members = await db.StaffOrderGroupMembers.AsNoTracking()
            .Where(m => m.OrderId == orderA || m.OrderId == orderB || m.OrderId == orderC)
            .ToListAsync();
        Assert.Equal(3, members.Count);
        Assert.Single(members.Select(m => m.GroupId).Distinct());
        Assert.Equal(secondGroupId, members[0].GroupId);
        Assert.NotEqual(firstGroupId, secondGroupId);
    }

    [Fact]
    public async Task CaseE_duplicate_orderIds_are_idempotent()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Item 2", 20000m);
        var table36 = await SeedTableAsync(db, "T36");
        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item2, quantity: 1);

        // Step 1: create [A, B]
        var first = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, first.statusCode);
        var firstGroupId = first.body.GetProperty("id").GetGuid();

        // Step 2: re-POST with [A, B, B] — must remain [A, B], same groupId
        var second = await CreateGroupAsync(_staffClient, new[] { orderA, orderB, orderB });
        Assert.Equal(HttpStatusCode.OK, second.statusCode);
        var secondGroupId = second.body.GetProperty("id").GetGuid();
        Assert.Equal(firstGroupId, secondGroupId);
        Assert.Equal(2, second.body.GetProperty("memberCount").GetInt32());

        // Only one membership row per (Group, Order).
        var members = await db.StaffOrderGroupMembers.AsNoTracking()
            .Where(m => m.OrderId == orderA || m.OrderId == orderB)
            .ToListAsync();
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task CaseF_pricing_uses_OrderItem_snapshot_not_MenuItem_Price()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item, menuPrice) = await SeedMenuItemAsync(db, "Latte", 65000m);
        var table36 = await SeedTableAsync(db, "T36");

        // Snapshot at 30k with quantity 2 → 60k, even though menuPrice changed to 65k after.
        var orderA = await SeedSubmittedOrderAsync(db, table36, item, quantity: 2, overrideUnitPrice: 30000m);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item, quantity: 1, overrideUnitPrice: 50000m);

        // Pretend the menu price changed after submit.
        item.Price = 99000m;
        await db.SaveChangesAsync();

        var groupRes = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, groupRes.statusCode);

        // 30k*2 + 50k*1 = 110k, NOT based on the current menu price.
        Assert.Equal(110000m, groupRes.body.GetProperty("aggregateTotal").GetDecimal());

        // Original OrderItems.UnitPrice remain unchanged.
        Assert.Equal(30000m, await db.OrderItems.Where(i => i.OrderId == orderA).Select(i => i.UnitPrice).FirstAsync());
        Assert.Equal(50000m, await db.OrderItems.Where(i => i.OrderId == orderB).Select(i => i.UnitPrice).FirstAsync());
    }

    [Fact]
    public async Task CaseG_non_eligible_order_is_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item, _) = await SeedMenuItemAsync(db, "Latte", 30000m);
        var table36 = await SeedTableAsync(db, "T36");
        var goodOrder = await SeedSubmittedOrderAsync(db, table36, item, quantity: 1);
        var cancelledOrderId = await SeedSubmittedOrderAsync(db, table36, item, quantity: 1);
        var cancelledOrder = await db.Orders.FirstAsync(o => o.Id == cancelledOrderId);
        cancelledOrder.Status = OrderStatus.Cancelled;
        cancelledOrder.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var res = await CreateGroupAsync(_staffClient, new[] { goodOrder, cancelledOrderId });
        Assert.Equal(HttpStatusCode.BadRequest, res.statusCode);

        var error = res.body.GetProperty("error").GetString();
        Assert.Equal("order_not_eligible", error);

        // Neither order should have been grouped.
        var members = await db.StaffOrderGroupMembers.AsNoTracking()
            .Where(m => m.OrderId == goodOrder || m.OrderId == cancelledOrderId)
            .ToListAsync();
        Assert.Empty(members);
    }

    [Fact]
    public async Task CaseH_groups_persist_across_board_refresh()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Item 2", 20000m);
        var table36 = await SeedTableAsync(db, "T36");
        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item2, quantity: 1);

        // Create group [A, B]
        var create = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        Assert.Equal(HttpStatusCode.OK, create.statusCode);
        var groupId = create.body.GetProperty("id").GetGuid();

        // Refresh the staff board; the group must still appear with both orders
        // and the same aggregate total. Filter to groups that contain our orders
        // because the test database accumulates state across fixtures.
        var board = await _staffClient.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var refreshedGroups = board.GetProperty("groups").EnumerateArray()
            .Where(g => g.GetProperty("orderIds").EnumerateArray()
                .Any(oid => oid.GetGuid() == orderA || oid.GetGuid() == orderB))
            .ToList();
        Assert.Single(refreshedGroups);
        var refreshed = refreshedGroups[0];
        Assert.Equal(groupId, refreshed.GetProperty("id").GetGuid());
        Assert.Equal(2, refreshed.GetProperty("memberCount").GetInt32());
        Assert.Equal(30000m, refreshed.GetProperty("aggregateTotal").GetDecimal());

        // The Orders are still listed individually (their underlying state is
        // preserved; grouping is a read-time view).
        var submitted = board.GetProperty("submitted").EnumerateArray().ToList();
        var ids = submitted.Select(o => o.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(orderA, ids);
        Assert.Contains(orderB, ids);
    }

    [Fact]
    public async Task CaseI_ungroup_dissolves_group_and_preserves_orders()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item1, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var (item2, _) = await SeedMenuItemAsync(db, "Item 2", 20000m);
        var table36 = await SeedTableAsync(db, "T36");
        var orderA = await SeedSubmittedOrderAsync(db, table36, item1, quantity: 1);
        var orderB = await SeedSubmittedOrderAsync(db, table36, item2, quantity: 1);

        var create = await CreateGroupAsync(_staffClient, new[] { orderA, orderB });
        var groupId = create.body.GetProperty("id").GetGuid();

        var del = await _staffClient.DeleteAsync($"/api/staff/order-groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        // Memberships for these orders are gone.
        var members = await db.StaffOrderGroupMembers.AsNoTracking()
            .Where(m => m.OrderId == orderA || m.OrderId == orderB)
            .ToListAsync();
        Assert.Empty(members);

        // Board still shows no groups that contain our orders; both orders remain individually.
        var board = await _staffClient.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var stillGrouped = board.GetProperty("groups").EnumerateArray()
            .Where(g => g.GetProperty("orderIds").EnumerateArray()
                .Any(oid => oid.GetGuid() == orderA || oid.GetGuid() == orderB))
            .ToList();
        Assert.Empty(stillGrouped);
        var submittedIds = board.GetProperty("submitted").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid())
            .ToList();
        Assert.Contains(orderA, submittedIds);
        Assert.Contains(orderB, submittedIds);

        // Original Orders' totals are unchanged.
        Assert.Equal(10000m, await db.Orders.Where(o => o.Id == orderA).Select(o => o.TotalAmount).FirstAsync());
        Assert.Equal(20000m, await db.Orders.Where(o => o.Id == orderB).Select(o => o.TotalAmount).FirstAsync());
    }

    [Fact]
    public async Task Unknown_order_id_is_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (item, _) = await SeedMenuItemAsync(db, "Item 1", 10000m);
        var table36 = await SeedTableAsync(db, "T36");
        var orderA = await SeedSubmittedOrderAsync(db, table36, item, quantity: 1);
        var missing = Guid.NewGuid();

        var res = await CreateGroupAsync(_staffClient, new[] { orderA, missing });
        Assert.Equal(HttpStatusCode.BadRequest, res.statusCode);
        Assert.Equal("unknown_order", res.body.GetProperty("error").GetString());
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static async Task LoginStaffAsync(HttpClient staffClient)
    {
        var get = await staffClient.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (!tokenMatch.Success)
            throw new InvalidOperationException("Could not extract __RequestVerificationToken.");

        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-staff-secret-16",
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };

        var post = await staffClient.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task<(HttpStatusCode statusCode, JsonElement body)> CreateGroupAsync(
        HttpClient client, IEnumerable<Guid> orderIds)
    {
        var res = await client.PostAsJsonAsync("/api/staff/order-groups", new { orderIds });
        var raw = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(raw))
        {
            return (res.StatusCode, default);
        }
        JsonElement body;
        try
        {
            body = JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Non-JSON response ({(int)res.StatusCode} {res.StatusCode}) body: {raw[..Math.Min(raw.Length, 200)]}",
                ex);
        }
        return (res.StatusCode, body);
    }

    private static async Task<(MenuItem item, decimal price)> SeedMenuItemAsync(AppDbContext db, string name, decimal price)
    {
        // Test container is shared across tests — suffix every name so we
        // never collide on the unique constraint enforced by MenuCategory.Name.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = new MenuCategory { Name = $"GroupTest-{suffix}", SortOrder = 999 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();
        var item = new MenuItem
        {
            CategoryId = category.Id,
            Name = $"{name}-{suffix}",
            Price = price,
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return (item, price);
    }

    private static async Task<Guid> SeedTableAsync(AppDbContext db, string displayCode)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        // Append a unique suffix to DisplayCode so we never collide on the
        // (VenueCode, DisplayCode) unique constraint while the container
        // accumulates rows between fixtures.
        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = $"{displayCode}-{suffix}",
            PublicSlug = $"annap-{displayCode.ToLowerInvariant()}-{suffix}",
            DisplayLabel = $"Test {displayCode}-{suffix}",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();
        return table.Id;
    }

    private static async Task<Guid> SeedSubmittedOrderAsync(
        AppDbContext db,
        Guid venueTableId,
        MenuItem menuItem,
        int quantity,
        decimal? overrideUnitPrice = null)
    {
        var table = await db.VenueTables.AsNoTracking().FirstAsync(t => t.Id == venueTableId);
        var now = DateTimeOffset.UtcNow;
        var unitPrice = overrideUnitPrice ?? menuItem.Price;
        var order = new Order
        {
            TableCode = table.DisplayCode,
            VenueTableId = table.Id,
            Status = OrderStatus.Submitted,
            CreatedAtUtc = now,
            StatusChangedAtUtc = now,
            PaymentMethod = "Cash"
        };
        order.Items.Add(new OrderItem
        {
            MenuItemId = menuItem.Id,
            MenuItemName = menuItem.Name,
            Quantity = quantity,
            UnitPrice = unitPrice
        });
        order.RecalculateTotals();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}