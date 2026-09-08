using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// End-to-end closure for Bank Transfer + Hot/Iced after Temperature schema registration.
/// </summary>
public sealed class BankTransferTemperaturePipelineTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Bank_transfer_hot_drink_persists_and_flows_to_completed()
    {
        var fixture = await SeedFixtureAsync();
        var idem = $"bt-temp-pipeline-{Guid.NewGuid():N}";

        var submit = await PostOrderAsync(fixture, idem, DrinkServingTemperature.Hot, OrderPaymentMethods.BankTransfer);
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
            Assert.Equal(OrderStatus.Submitted, order.Status);
            Assert.Null(order.PaidAtUtc);
            Assert.Equal(OrderPaymentMethods.BankTransfer, order.PaymentMethod);
            Assert.Equal(DrinkServingTemperature.Hot, order.Items.Single().Temperature);

            var missing = await PaymentWorkflowSchemaGuard.GetMissingColumnsAsync(db);
            Assert.Empty(missing);
        }

        var qr = await _guest.GetAsync($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token!)}");
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        var qrBody = await qr.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(qrBody.ValueKind is JsonValueKind.Object);
        Assert.True(
            qrBody.TryGetProperty("qrImageUrl", out var qrUrl) && qrUrl.ValueKind == JsonValueKind.String
            || qrBody.TryGetProperty("status", out _),
            "transfer-qr payload must expose QR or status");

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);

        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        Assert.Equal("BankTransfer", submitted.GetProperty("paymentMethod").GetString());
        var line = submitted.GetProperty("items").EnumerateArray().First();
        Assert.Equal(DrinkServingTemperature.Hot, line.GetProperty("temperature").GetString());

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.NotNull(order.PaidAtUtc);
        }

        var paidBoard = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = paidBoard.GetProperty("paid").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        var itemId = paid.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/complete", new { })).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
            Assert.Equal(OrderStatus.Completed, order.Status);
            Assert.NotNull(order.CompletedAtUtc);
            Assert.Equal(DrinkServingTemperature.Hot, order.Items.Single().Temperature);
        }
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string temperature,
        string paymentMethod)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["paymentMethod"] = paymentMethod,
            ["items"] = new[]
            {
                new
                {
                    menuItemId = fixture.MenuItemId,
                    quantity = 1,
                    notes = (string?)null,
                    temperature
                }
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _guest.SendAsync(req);
    }

    private static async Task LoginStaffAsync(HttpClient client)
    {
        var get = await client.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-checkout-secret-16",
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
