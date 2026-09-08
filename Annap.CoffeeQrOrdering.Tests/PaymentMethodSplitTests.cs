using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class PaymentMethodSplitTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Theory]
    [InlineData(OrderPaymentMethods.Cash)]
    [InlineData(OrderPaymentMethods.Card)]
    [InlineData(OrderPaymentMethods.BankTransfer)]
    public async Task Guest_submit_persists_split_payment_method(string paymentMethod)
    {
        var fixture = await SeedFixtureAsync();
        var res = await PostOrderAsync(fixture, $"pms-submit-{paymentMethod}", paymentMethod);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(paymentMethod, order.PaymentMethod);
    }

    [Fact]
    public async Task Guest_submit_without_payment_method_defaults_to_cash()
    {
        var fixture = await SeedFixtureAsync();
        var res = await PostOrderAsync(fixture, "pms-default-cash", paymentMethod: null);
        res.EnsureSuccessStatusCode();
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, order.PaymentMethod);
    }

    [Theory]
    [InlineData(OrderPaymentMethods.Cash, "Tiền mặt", "Tiền mặt · chờ thanh toán")]
    [InlineData(OrderPaymentMethods.Card, "Thẻ", "Thẻ · chờ thanh toán")]
    [InlineData(OrderPaymentMethods.BankTransfer, "Chuyển khoản", "Chuyển khoản · chờ xác nhận")]
    public async Task Staff_board_submitted_exposes_payment_method_labels(
        string paymentMethod,
        string labelVi,
        string pendingBadge)
    {
        var fixture = await SeedFixtureAsync();
        var submit = await PostOrderAsync(fixture, $"pms-board-sub-{paymentMethod}", paymentMethod);
        submit.EnsureSuccessStatusCode();
        var orderId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(paymentMethod, submitted.GetProperty("paymentMethod").GetString());
        Assert.Equal(labelVi, submitted.GetProperty("paymentMethodLabelVi").GetString());
        Assert.Equal(pendingBadge, submitted.GetProperty("pendingPaymentBadgeVi").GetString());
    }

    [Theory]
    [InlineData(OrderPaymentMethods.Cash, "Tiền mặt")]
    [InlineData(OrderPaymentMethods.Card, "Thẻ")]
    [InlineData(OrderPaymentMethods.BankTransfer, "Chuyển khoản")]
    public async Task Staff_board_paid_and_completed_render_exact_payment_method(string paymentMethod, string labelVi)
    {
        var fixture = await SeedFixtureAsync();
        var submit = await PostOrderAsync(fixture, $"pms-paid-{paymentMethod}", paymentMethod);
        submit.EnsureSuccessStatusCode();
        var orderId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/complete", new { })).StatusCode);

        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var completed = board.GetProperty("completed").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(paymentMethod, completed.GetProperty("paymentMethod").GetString());
        Assert.Equal(labelVi, completed.GetProperty("paymentMethodLabelVi").GetString());
    }

    [Fact]
    public async Task Shift_close_splits_cash_card_and_bank_transfer_separately()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var cash = ShiftClosePaidOrder(fixture, OrderPaymentMethods.Cash, 50000m);
        var card = ShiftClosePaidOrder(fixture, OrderPaymentMethods.Card, 60000m);
        var bank = ShiftClosePaidOrder(fixture, OrderPaymentMethods.BankTransfer, 70000m);
        db.Orders.AddRange(cash, card, bank);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(ShiftCloseTestHelper.TestCheckoutPrincipal());
        Assert.Equal(50000m, preview.CashAmount);
        Assert.Equal(60000m, preview.CardAmount);
        Assert.Equal(70000m, preview.BankTransferAmount);
        Assert.Equal(1, preview.CashOrders);
        Assert.Equal(1, preview.CardOrders);
        Assert.Equal(1, preview.BankTransferOrders);
        Assert.Equal(180000m, preview.TotalGrossAmount);

        var employee = preview.Employees.Single();
        Assert.Equal(50000m, employee.CashAmount);
        Assert.Equal(60000m, employee.CardAmount);
        Assert.Equal(70000m, employee.BankTransferAmount);
    }

    [Fact]
    public async Task Legacy_cash_or_card_at_counter_does_not_break_shift_close()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var legacy = ShiftClosePaidOrder(fixture, OrderPaymentMethods.CashOrCardAtCounter, 45000m);
        db.Orders.Add(legacy);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(ShiftCloseTestHelper.TestCheckoutPrincipal());
        Assert.Equal(45000m, preview.LegacyCashOrCardAmount);
        Assert.Equal(1, preview.LegacyCashOrCardOrders);
        Assert.Equal(0m, preview.CashAmount);
        Assert.Equal(0m, preview.CardAmount);
        Assert.Contains("Tiền mặt/thẻ cũ", svc.BuildCopyText(preview), StringComparison.Ordinal);
    }

    private static Domain.Entities.Order ShiftClosePaidOrder(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string paymentMethod,
        decimal amount) =>
        new()
        {
            VenueTableId = fixture.VenueTableId,
            TableCode = "T01",
            Status = Domain.Entities.OrderStatus.Paid,
            PaymentMethod = paymentMethod,
            TotalAmount = amount,
            PaidAtUtc = ShiftCloseTestHelper.PaidAfterBoundary(),
            PaymentConfirmedBy = "Test",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
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
        Assert.True(tokenMatch.Success);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-staff-secret-16",
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
