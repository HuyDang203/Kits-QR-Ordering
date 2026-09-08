using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class BankTransferAcbUnfreezeTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public void Annap_acb_options_are_configured_for_vietqr()
    {
        var opts = AnnapAcbOptions();
        Assert.True(opts.IsConfigured);
        Assert.Equal("970000", opts.BankBin);
        Assert.Equal("TEST BANK", opts.BankName);
        Assert.Equal("0000000000", opts.AccountNumber);
        Assert.Equal("TEST COFFEE ACCOUNT", opts.AccountName);
    }

    [Fact]
    public void Transfer_qr_pending_includes_acb_bank_details_and_memo()
    {
        var builder = new BankTransferQrBuilder(Options.Create(AnnapAcbOptions()));
        var order = SampleBankOrder(total: 165000m);
        order.BillNumber = "A8402E1E3";

        var dto = builder.Build(order);
        Assert.True(dto.Enabled);
        Assert.Equal("pending", dto.Status);
        Assert.Equal("TEST BANK", dto.BankName);
        Assert.Equal("970000", dto.BankBin);
        Assert.Equal("0000000000", dto.AccountNumber);
        Assert.Equal("TEST COFFEE ACCOUNT", dto.AccountName);
        Assert.Equal(165000, dto.Amount);
        Assert.Equal("KITS A8402E1E3", dto.Memo);
        Assert.Contains("nhân viên xác nhận nhanh hơn", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transfer_qr_url_contains_acb_bin_account_amount_and_encoded_memo()
    {
        var builder = new BankTransferQrBuilder(Options.Create(AnnapAcbOptions()));
        var order = SampleBankOrder(total: 165000m);
        order.BillNumber = "A8402E1E3";
        var memo = builder.BuildMemoForOrder(order);
        var url = builder.BuildQrImageUrl(order, order.BillNumber!, memo)!;

        Assert.Contains("970000-0000000000", url, StringComparison.Ordinal);
        Assert.Contains("amount=165000", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(memo), url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("TEST COFFEE ACCOUNT"), url, StringComparison.Ordinal);
        Assert.DoesNotContain("token", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guest", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BankTransfer_submit_remains_submitted_without_webhook()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _) = await SubmitWithTokenAsync(fixture, "acb-submit-1", OrderPaymentMethods.BankTransfer);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Null(order.PaidAtUtc);
        Assert.Equal(OrderPaymentMethods.BankTransfer, order.PaymentMethod);
    }

    [Fact]
    public async Task Transfer_qr_api_returns_acb_fields_for_bank_transfer_order()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "acb-qr-api-1", OrderPaymentMethods.BankTransfer);

        var qr = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");

        Assert.True(qr.GetProperty("enabled").GetBoolean());
        Assert.Equal("TEST BANK", qr.GetProperty("bankName").GetString());
        Assert.Equal("970000", qr.GetProperty("bankBin").GetString());
        Assert.Equal("0000000000", qr.GetProperty("accountNumber").GetString());
        Assert.Equal("TEST COFFEE ACCOUNT", qr.GetProperty("accountName").GetString());
        Assert.StartsWith("KITS ", qr.GetProperty("memo").GetString());
        Assert.Contains("970000-0000000000", qr.GetProperty("qrImageUrl").GetString());
    }

    [Fact]
    public async Task Guest_availability_returns_enabled_with_test_acb_factory_config()
    {
        var avail = await _client.GetFromJsonAsync<JsonElement>("/api/guest/bank-transfer");
        Assert.True(avail.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Staff_manual_mark_paid_works_for_bank_transfer_order()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "acb-mark-paid-1", OrderPaymentMethods.BankTransfer);

        var checkout = factory.CreateClient();
        checkout.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAtUtc);
    }

    [Fact]
    public async Task Cash_card_submit_still_works_when_bank_transfer_enabled()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "acb-cash-1", OrderPaymentMethods.CashOrCardAtCounter);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.CashOrCardAtCounter, order.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    private static BankTransferOptions AnnapAcbOptions() => new()
    {
        Enabled = true,
        Provider = "VietQR",
        BankBin = "970000",
        BankName = "TEST BANK",
        AccountNumber = "0000000000",
        AccountName = "TEST COFFEE ACCOUNT",
        DescriptionTemplate = "KITS {Reference}",
        QrImageUrlTemplate =
            "https://img.vietqr.io/image/{bankBin}-{accountNumber}-compact2.png?amount={amount}&addInfo={memo}&accountName={accountName}"
    };

    private static Order SampleBankOrder(decimal total) => new()
    {
        Id = Guid.NewGuid(),
        TableCode = "T01",
        Status = OrderStatus.Submitted,
        PaymentMethod = OrderPaymentMethods.BankTransfer,
        TotalAmount = total,
        BillNumber = "A8402E1E3"
    };

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<Guid> SubmitOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod)
    {
        var (orderId, _) = await SubmitWithTokenAsync(fixture, idemKey, paymentMethod);
        return orderId;
    }

    private async Task<(Guid OrderId, string Token)> SubmitWithTokenAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["paymentMethod"] = paymentMethod,
            ["items"] = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), body.GetProperty("guestSessionToken").GetString()!);
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
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
