using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Integration tests for the Order Fulfillment Mode (DineIn / Takeaway) feature.
/// Covers acceptance criteria:
///   1. DineIn accepted (with table)
///   2. Takeaway accepted (without table)
///   3. Unknown fulfillment mode rejected
///   4. Dine-in without table rejected
///   5. Dine-in with valid table succeeds
///   6. Takeaway without table succeeds
///   7. Takeaway with valid table succeeds
///   8. Fulfillment mode persisted on Order
///   9. Legacy null fulfillment mode remains readable
///  10. Payment behavior unchanged when fulfillment is provided
/// </summary>
public sealed class FulfillmentModeIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DineIn_with_valid_table_succeeds_and_persists_DineIn()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-dinein-ok-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "DineIn");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("DineIn", body.GetProperty("fulfillmentMode").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FirstAsync(o => o.SubmitIdempotencyKey == idemKey);
        Assert.Equal("DineIn", order.FulfillmentMode);
        Assert.Equal(fixture.VenueTableId, order.VenueTableId);
    }

    [Fact]
    public async Task Takeaway_without_table_succeeds_and_persists_Takeaway()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-takeaway-no-table-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "Takeaway", includeTableId: false);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Takeaway", body.GetProperty("fulfillmentMode").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FirstAsync(o => o.SubmitIdempotencyKey == idemKey);
        Assert.Equal("Takeaway", order.FulfillmentMode);
        Assert.Null(order.VenueTableId);
    }

    [Fact]
    public async Task Takeaway_with_valid_table_context_does_not_fail()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-takeaway-with-table-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "Takeaway", includeTableId: true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Takeaway", body.GetProperty("fulfillmentMode").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FirstAsync(o => o.SubmitIdempotencyKey == idemKey);
        Assert.Equal("Takeaway", order.FulfillmentMode);
        Assert.Equal(fixture.VenueTableId, order.VenueTableId);
    }

    [Fact]
    public async Task DineIn_without_table_returns_400_with_existing_message()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-dinein-no-table-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "DineIn", includeTableId: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("scan the table QR", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_fulfillment_mode_returns_400()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-unknown-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "DriveThru", includeTableId: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("fulfillmentMode", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_fulfillment_mode_returns_400()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-missing-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: null, includeTableId: true);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("fulfillmentMode", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Legacy_order_with_null_fulfillment_is_readable_via_bill_endpoint()
    {
        var fixture = await SeedFreshFixtureAsync();

        // Insert a legacy order (no fulfillment mode) directly via the DB.
        Guid orderId;
        string guestToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            guestToken = Guid.NewGuid().ToString("N");
            var legacy = new Order
            {
                VenueTableId = fixture.VenueTableId,
                TableCode = "T07",
                GuestSessionToken = guestToken,
                SubmitIdempotencyKey = "legacy-" + Guid.NewGuid().ToString("N"),
                Status = OrderStatus.Paid,
                StatusChangedAtUtc = DateTimeOffset.UtcNow,
                PaidAtUtc = DateTimeOffset.UtcNow,
                PaymentMethod = "Cash",
                // FulfillmentMode intentionally left null (legacy).
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        MenuItemId = fixture.MenuItemId,
                        Quantity = 1,
                        UnitPrice = fixture.MenuPrice,
                        MenuItemName = fixture.MenuItemName
                    }
                }
            };
            legacy.RecalculateTotals();
            db.Orders.Add(legacy);
            await db.SaveChangesAsync();
            orderId = legacy.Id;
        }

        // Hit the public bill endpoint with the guest session token.
        var url = $"/api/orders/{orderId:D}/bill?token={Uri.EscapeDataString(guestToken)}";
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("fulfillmentMode", out var fm));
        Assert.True(fm.ValueKind == JsonValueKind.Null, "Legacy order should have null fulfillmentMode.");
        Assert.True(body.TryGetProperty("fulfillmentModeLabelVi", out var fmLabel));
        Assert.Equal("Không xác định", fmLabel.GetString());
    }

    [Fact]
    public async Task Existing_payment_methods_still_work_with_DineIn()
    {
        var fixture = await SeedFreshFixtureAsync();
        const string idemKey = "fulfillment-payment-cash-1";

        var response = await PostOrderAsync(fixture, idemKey, fulfillmentMode: "DineIn", paymentMethod: "Cash");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FirstAsync(o => o.SubmitIdempotencyKey == idemKey);
        Assert.Equal("Cash", order.PaymentMethod);
        Assert.Equal("DineIn", order.FulfillmentMode);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idempotencyKey,
        string? fulfillmentMode = "DineIn",
        string? paymentMethod = "Cash",
        bool includeTableId = true,
        int quantity = 1,
        Guid? menuItemId = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["items"] = new[]
            {
                new
                {
                    menuItemId = menuItemId ?? fixture.MenuItemId,
                    quantity
                }
            }
        };
        if (includeTableId)
        {
            payload["venueTableId"] = fixture.VenueTableId;
        }
        else
        {
            payload["venueTableId"] = null;
        }
        if (paymentMethod is not null)
        {
            payload["paymentMethod"] = paymentMethod;
        }
        if (fulfillmentMode is not null)
        {
            payload["fulfillmentMode"] = fulfillmentMode;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
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
