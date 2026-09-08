using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class PaymentFlowPhase1Tests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(OrderPaymentMethods.Cash)]
    [InlineData(OrderPaymentMethods.Card)]
    [InlineData(OrderPaymentMethods.BankTransfer)]
    [InlineData(OrderPaymentMethods.CashOrCardAtCounter)]
    public async Task Submit_persists_payment_method_and_stays_submitted(string paymentMethod)
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, $"pf1-submit-{paymentMethod}", paymentMethod);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(paymentMethod, order.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Null(order.PaidAtUtc);
        Assert.Null(order.BillNumber);
    }

    [Fact]
    public async Task BankTransfer_submit_persists_bill_number_reference()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "pf1-submit-bank-ref", OrderPaymentMethods.BankTransfer);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.BankTransfer, order.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Null(order.PaidAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(order.BillNumber));
        Assert.StartsWith("A", order.BillNumber);
    }

    [Fact]
    public async Task Submit_without_payment_method_defaults_to_cash()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "pf1-default-method", paymentMethod: null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, order.PaymentMethod);
    }

    [Fact]
    public async Task Submit_with_invalid_payment_method_returns_400()
    {
        var fixture = await SeedFixtureAsync();
        var res = await PostOrderRawAsync(fixture, "pf1-invalid-method", "CryptoCoin");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Guest_track_pending_shows_check_bill_not_paid_receipt()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitOrderWithTokenAsync(
            fixture,
            "pf1-track-check",
            OrderPaymentMethods.BankTransfer);

        var pending = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(pending.GetProperty("pendingPayment").GetBoolean());
        Assert.False(pending.GetProperty("showBill").GetBoolean());
        Assert.True(pending.GetProperty("showCheckBill").GetBoolean());
        Assert.True(pending.TryGetProperty("checkBill", out var checkBill));
        Assert.Equal("provisional", checkBill.GetProperty("billKind").GetString());
        Assert.Equal("Phiếu kiểm đơn", checkBill.GetProperty("titleVi").GetString());
        Assert.Equal(OrderPaymentMethods.BankTransfer, pending.GetProperty("paymentMethod").GetString());
    }

    [Fact]
    public async Task Guest_track_paid_shows_paid_receipt_title()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitOrderWithTokenAsync(
            fixture,
            "pf1-track-paid",
            OrderPaymentMethods.CashOrCardAtCounter);

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var paid = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.False(paid.GetProperty("pendingPayment").GetBoolean());
        Assert.True(paid.GetProperty("showBill").GetBoolean());
        Assert.False(paid.GetProperty("showCheckBill").GetBoolean());
        var bill = paid.GetProperty("bill");
        Assert.Equal("paid", bill.GetProperty("billKind").GetString());
        Assert.Equal("Hóa đơn đã thanh toán", bill.GetProperty("titleVi").GetString());
    }

    [Fact]
    public async Task Cash_and_bank_orders_stay_in_submitted_column_until_mark_paid()
    {
        var fixture = await SeedFixtureAsync();
        var cashId = await SubmitOrderAsync(fixture, "pf1-col-cash", OrderPaymentMethods.Cash);
        var bankId = await SubmitOrderAsync(fixture, "pf1-col-bank", OrderPaymentMethods.BankTransfer);

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(cashId, submitted);
        Assert.Contains(bankId, submitted);

        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{bankId}/mark-paid", new { })).StatusCode);
        var boardAfter = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = boardAfter.GetProperty("paid").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(bankId, paid);
    }

    [Fact]
    public async Task Barista_cannot_mark_pending_bank_transfer_paid()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "pf1-barista-mark-1", OrderPaymentMethods.BankTransfer);

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");

        var res = await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Null(order.PaidAtUtc);
    }

    [Theory]
    [InlineData("Cash", "cash")]
    [InlineData("Card", "card")]
    [InlineData("CashOrCardAtCounter", "counter")]
    [InlineData("BankTransfer", "transfer")]
    public void OrderPaymentMethods_normalizes_aliases(string expected, string alias)
    {
        Assert.Equal(expected, OrderPaymentMethods.Normalize(alias));
    }

    [Fact]
    public void OrderBillHelper_build_check_bill_uses_provisional_labels()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TableCode = "T01",
            Status = OrderStatus.Submitted,
            PaymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    MenuItemName = "Latte",
                    Quantity = 1,
                    UnitPrice = 45000m
                }
            ]
        };
        order.RecalculateTotals();
        var bill = OrderBillHelper.BuildCheckBill(order);
        Assert.Equal("provisional", bill.BillKind);
        Assert.Equal("Tạm tính", bill.TotalLabelVi);
        Assert.Equal("Chờ thanh toán", bill.PaymentStatusLabelVi);
        Assert.Equal("Tiền mặt/thẻ tại quầy", bill.PaymentMethodLabelVi);
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<Guid> SubmitOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string? paymentMethod,
        int quantity = 1)
    {
        var (orderId, _) = await SubmitOrderWithTokenAsync(fixture, idemKey, paymentMethod, quantity);
        return orderId;
    }

    private async Task<(Guid OrderId, string Token)> SubmitOrderWithTokenAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string? paymentMethod,
        int quantity = 1)
    {
        var res = await PostOrderRawAsync(fixture, idemKey, paymentMethod, quantity);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), body.GetProperty("guestSessionToken").GetString()!);
    }

    private async Task<HttpResponseMessage> PostOrderRawAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string? paymentMethod,
        int quantity = 1)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["items"] = new[] { new { menuItemId = fixture.MenuItemId, quantity, notes = (string?)null } }
        };
        if (paymentMethod is not null)
            payload["paymentMethod"] = paymentMethod;

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _client.SendAsync(req);
    }

    private static async Task LoginStaffAsync(HttpClient client, string user, string password)
    {
        var get = await client.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        var token = tokenMatch.Groups["v"].Value;
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
