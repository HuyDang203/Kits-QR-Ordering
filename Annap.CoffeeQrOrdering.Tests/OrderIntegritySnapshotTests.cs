using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class OrderIntegritySnapshotTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Order_submission_stores_menu_item_name_snapshot_and_keeps_it_after_rename()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "snap-rename-1";

        var post = await PostOrderAsync(fixture, idemKey);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var postBody = await post.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = postBody.GetProperty("id").GetGuid();
        var token = postBody.GetProperty("guestSessionToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var snapshot = await db.OrderItems
                .Where(i => i.OrderId == orderId)
                .Select(i => i.MenuItemName)
                .SingleAsync();
            Assert.Equal(fixture.MenuItemName, snapshot);

            var menu = await db.MenuItems.FirstAsync(m => m.Id == fixture.MenuItemId);
            menu.Name = "Test Latte (Renamed)";
            await db.SaveChangesAsync();
        }

        var get = await _client.GetAsync($"/api/orders/{orderId}?token={Uri.EscapeDataString(token!)}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        var items = getBody.TryGetProperty("items", out var itemsCamel)
            ? itemsCamel
            : getBody.GetProperty("Items");
        var first = items[0];
        var returnedName = first.TryGetProperty("name", out var nameCamel)
            ? nameCamel.GetString()
            : first.GetProperty("Name").GetString();
        Assert.Equal(fixture.MenuItemName, returnedName);
    }

    [Fact]
    public async Task Menu_item_delete_is_blocked_when_order_items_exist()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "delete-block-1";

        var post = await PostOrderAsync(fixture, idemKey);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var menu = await db.MenuItems.FirstAsync(m => m.Id == fixture.MenuItemId);
            db.MenuItems.Remove(menu);

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Quantity_le_0_is_rejected()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "qty-0-1";

        var res = await _client.PostAsJsonAsync("/api/orders", new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 0 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("Quantity", error.GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_order_status_is_blocked_by_database_constraint()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "status-invalid-1";

        var post = await PostOrderAsync(fixture, idemKey);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var order = await db.Orders.FirstAsync(o => o.SubmitIdempotencyKey == idemKey);
            order.Status = (OrderStatus)999;

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFreshFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idempotencyKey,
        int quantity = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                venueTableId = fixture.VenueTableId,
                idempotencyKey,
                items = new[] { new { menuItemId = fixture.MenuItemId, quantity } }
            })
        };

        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }
}

