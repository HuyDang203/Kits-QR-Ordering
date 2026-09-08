using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class OrderSubmitIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Submit_without_idempotency_key_returns_400()
    {
        var fixture = await SeedFreshFixtureAsync();

        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            venueTableId = fixture.VenueTableId,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("receipt key", error.GetString(), StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await OrderTestSeedHelper.CountOrdersAsync(db));
    }

    [Fact]
    public async Task Submit_with_idempotency_key_defaults_prepared_quantity_to_zero()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "integration-submit-prep-default-1";

        var response = await PostOrderAsync(fixture, idemKey, quantity: 2);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.SingleAsync(i => i.OrderId == orderId);
        Assert.Equal(0, item.PreparedQuantity);
        Assert.Null(item.PreparedAtUtc);
        Assert.Null(item.PreparedBy);
    }

    [Fact]
    public async Task Submit_with_idempotency_key_succeeds_and_uses_server_price()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "integration-submit-success-1";

        var response = await PostOrderAsync(fixture, idemKey, quantity: 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var orderIdEl));
        var orderId = orderIdEl.GetGuid();

        Assert.True(body.TryGetProperty("totalAmount", out var total));
        Assert.Equal(fixture.MenuPrice * 2, total.GetDecimal());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitPrice = await OrderTestSeedHelper.GetOrderLineUnitPriceAsync(db, orderId);
        Assert.Equal(fixture.MenuPrice, unitPrice);
    }

    [Fact]
    public async Task Submit_replay_same_idempotency_key_does_not_duplicate_order()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "integration-submit-replay-1";

        var first = await PostOrderAsync(fixture, idemKey);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstOrderId = firstBody.GetProperty("id").GetGuid();

        var second = await PostOrderAsync(fixture, idemKey);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondBody.GetProperty("replay").GetBoolean());
        Assert.Equal(firstOrderId, secondBody.GetProperty("id").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.SubmitIdempotencyKey == idemKey));
    }

    [Fact]
    public async Task Submit_invalid_menu_item_returns_400()
    {
        var fixture = await SeedFreshFixtureAsync();

        var response = await PostOrderAsync(fixture, "integration-invalid-menu-1", menuItemId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("menu", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_too_many_line_items_returns_400()
    {
        var fixture = await SeedFreshFixtureAsync();
        var items = Enumerable.Range(0, OrderSubmitLimits.MaxLineItems + 1)
            .Select(_ => new { menuItemId = fixture.MenuItemId, quantity = 1 })
            .ToArray();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                venueTableId = fixture.VenueTableId,
                idempotencyKey = "integration-too-many-lines-1",
                items
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "integration-too-many-lines-1");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("20", error.GetString(), StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idempotencyKey,
        int quantity = 1,
        Guid? menuItemId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                venueTableId = fixture.VenueTableId,
                idempotencyKey,
                items = new[]
                {
                    new { menuItemId = menuItemId ?? fixture.MenuItemId, quantity }
                }
            })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFreshFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }
}
