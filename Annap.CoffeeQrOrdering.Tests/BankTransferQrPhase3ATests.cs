using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class BankTransferQrPhase3ATests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public void Transfer_qr_unavailable_when_config_disabled()
    {
        var builder = CreateBuilder(new BankTransferOptions { Enabled = false, BankBin = "970422", AccountNumber = "1", AccountName = "X" });
        var order = SampleBankOrder();
        var dto = builder.Build(order);
        Assert.False(dto.Enabled);
        Assert.Equal("unavailable", dto.Status);
    }

    [Fact]
    public void Transfer_qr_unavailable_when_required_config_missing()
    {
        var builder = CreateBuilder(new BankTransferOptions { Enabled = true, BankBin = "", AccountNumber = "", AccountName = "" });
        var dto = builder.Build(SampleBankOrder());
        Assert.False(dto.Enabled);
    }

    [Fact]
    public void Transfer_qr_returns_pending_data_when_config_valid()
    {
        var builder = CreateBuilder(ValidOptions());
        var order = SampleBankOrder(total: 65000m);
        order.BillNumber = "AEDF18E3";
        var dto = builder.Build(order);
        Assert.True(dto.Enabled);
        Assert.Equal("pending", dto.Status);
        Assert.Equal(65000, dto.Amount);
        Assert.Equal("AEDF18E3", dto.Reference);
        Assert.Equal("KITS AEDF18E3", dto.Memo);
        Assert.Contains("970416", dto.QrImageUrl);
        Assert.Contains("65000", dto.QrImageUrl);
        Assert.DoesNotContain("guest", dto.QrImageUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BankTransfer_submit_persists_stable_bill_number()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _) = await SubmitWithTokenAsync(fixture, "bt-ref-1", OrderPaymentMethods.BankTransfer);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.False(string.IsNullOrWhiteSpace(order.BillNumber));
    }

    [Fact]
    public async Task Mark_paid_does_not_overwrite_bank_transfer_reference()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _) = await SubmitWithTokenAsync(fixture, "bt-ref-2", OrderPaymentMethods.BankTransfer);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).BillNumber;

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var after = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(before, after.BillNumber);
    }

    [Fact]
    public void Transfer_memo_uses_configured_description_template()
    {
        var opts = ValidOptions();
        opts.DescriptionTemplate = "CAFE {Reference}";
        var builder = CreateBuilder(opts);
        Assert.Equal("CAFE A123", builder.BuildMemo("A123"));
    }

    [Fact]
    public void Custom_description_template_uses_reference_and_table_code()
    {
        var opts = ValidOptions();
        opts.DescriptionTemplate = "KITS-{Reference}-{TableCode}";
        var builder = CreateBuilder(opts);
        var order = SampleBankOrder();
        order.TableCode = "T01";
        order.BillNumber = "AEDF18E3";
        Assert.Equal("KITS-AEDF18E3-T01", builder.BuildMemoForOrder(order));
        var dto = builder.Build(order);
        Assert.Equal("KITS-AEDF18E3-T01", dto.Memo);
    }

    [Fact]
    public void Qr_url_contains_exact_amount_and_encoded_query_params()
    {
        var builder = CreateBuilder(ValidOptions());
        var order = SampleBankOrder(total: 65000m);
        order.BillNumber = "AEDF18E3";
        var memo = builder.BuildMemoForOrder(order);
        var url = builder.BuildQrImageUrl(order, "AEDF18E3", memo)!;
        Assert.StartsWith("https://", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("amount=65000", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(memo), url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("TEST COFFEE ACCOUNT"), url, StringComparison.Ordinal);
        Assert.Contains("970000-0000000000", url, StringComparison.Ordinal);
        Assert.DoesNotContain("guest", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guest_availability_returns_enabled_when_configured()
    {
        var avail = await _client.GetFromJsonAsync<JsonElement>("/api/guest/bank-transfer");
        Assert.True(avail.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Staff_board_transfer_memo_matches_guest_transfer_qr_and_track_memo()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-memo-match-1", OrderPaymentMethods.BankTransfer);

        var qr = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        var qrMemo = qr.GetProperty("memo").GetString();

        var track = await _client.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        var trackMemo = track.GetProperty("transferQr").GetProperty("memo").GetString();

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var cardMemo = board.GetProperty("submitted").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId)
            .GetProperty("transferMemo").GetString();

        Assert.False(string.IsNullOrWhiteSpace(qrMemo));
        Assert.Equal(qrMemo, trackMemo);
        Assert.Equal(qrMemo, cardMemo);
        Assert.DoesNotContain(token, qrMemo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mark_paid_does_not_change_transfer_memo()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-memo-paid-1", OrderPaymentMethods.BankTransfer);
        var before = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        var memoBefore = before.GetProperty("memo").GetString();

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var after = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.Equal("paid", after.GetProperty("status").GetString());
        Assert.Equal(memoBefore, after.GetProperty("memo").GetString());
    }

    [Fact]
    public async Task Cash_order_staff_board_has_no_transfer_memo()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "bt-cash-memo-1", OrderPaymentMethods.CashOrCardAtCounter);
        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var card = board.GetProperty("submitted").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        if (card.TryGetProperty("transferMemo", out var memo))
            Assert.True(memo.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || string.IsNullOrWhiteSpace(memo.GetString()));
    }

    [Fact]
    public async Task Transfer_qr_amount_matches_locked_order_total_after_menu_price_change()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-amount-1", OrderPaymentMethods.BankTransfer, quantity: 2);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var menu = await db.MenuItems.FirstAsync(m => m.Id == fixture.MenuItemId);
        menu.Price = 999_999m;
        await db.SaveChangesAsync();

        var qr = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.True(qr.GetProperty("enabled").GetBoolean());
        Assert.Equal((long)(fixture.MenuPrice * 2), qr.GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task Transfer_qr_rejects_missing_token()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "bt-token-miss", OrderPaymentMethods.BankTransfer);
        var res = await _client.GetAsync($"/api/orders/{orderId}/transfer-qr");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Transfer_qr_rejects_token_for_another_order()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _) = await SubmitWithTokenAsync(fixture, "bt-token-a", OrderPaymentMethods.BankTransfer);
        var (_, otherToken) = await SubmitWithTokenAsync(fixture, "bt-token-b", OrderPaymentMethods.BankTransfer);
        var res = await _client.GetAsync($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(otherToken)}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Transfer_qr_rejects_non_bank_transfer_order()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-cash", OrderPaymentMethods.CashOrCardAtCounter);
        var qr = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.False(qr.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Pending_bank_track_includes_transfer_qr_section()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-track-1", OrderPaymentMethods.BankTransfer);
        var track = await _client.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(track.GetProperty("pendingPayment").GetBoolean());
        Assert.True(track.TryGetProperty("transferQr", out var transferQr));
        Assert.True(transferQr.GetProperty("enabled").GetBoolean());
        Assert.Equal("pending", transferQr.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pending_cash_track_does_not_include_transfer_qr()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-track-cash", OrderPaymentMethods.CashOrCardAtCounter);
        var track = await _client.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(track.GetProperty("pendingPayment").GetBoolean());
        Assert.False(track.TryGetProperty("transferQr", out _) && track.GetProperty("transferQr").ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Paid_bank_transfer_hides_pending_transfer_and_shows_paid_receipt()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitWithTokenAsync(fixture, "bt-paid-1", OrderPaymentMethods.BankTransfer);
        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var track = await _client.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.False(track.GetProperty("pendingPayment").GetBoolean());
        Assert.True(track.GetProperty("showBill").GetBoolean());
        var qr = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.Equal("paid", qr.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Staff_board_bank_transfer_card_includes_transfer_memo()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "bt-board-1", OrderPaymentMethods.BankTransfer);
        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var card = board.GetProperty("submitted").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal("BankTransfer", card.GetProperty("paymentMethod").GetString());
        Assert.StartsWith("KITS ", card.GetProperty("transferMemo").GetString());
    }

    [Fact]
    public async Task Checkout_can_mark_bank_transfer_paid()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "bt-mark-1", OrderPaymentMethods.BankTransfer);
        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
    }

    [Fact]
    public async Task Barista_cannot_mark_bank_transfer_paid()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "bt-barista-1", OrderPaymentMethods.BankTransfer);
        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        Assert.Equal(HttpStatusCode.Forbidden, (await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
    }

    [Fact]
    public void Transfer_qr_endpoint_unavailable_when_bank_transfer_disabled_in_config()
    {
        var builder = CreateBuilder(new BankTransferOptions
        {
            Enabled = false,
            BankBin = "970422",
            AccountNumber = "0123456789",
            AccountName = "ANNAP"
        });
        var dto = builder.Build(SampleBankOrder());
        Assert.False(dto.Enabled);
        Assert.Contains("Chuyển khoản hiện chưa khả dụng", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Guid OrderId, string Token)> SubmitWithTokenAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod,
        int quantity = 1) =>
        await SubmitWithTokenAsync(_client, fixture, idemKey, paymentMethod, quantity);

    private static async Task<Guid> SubmitOrderAsync(
        HttpClient client,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod)
    {
        var (orderId, _) = await SubmitWithTokenAsync(client, fixture, idemKey, paymentMethod);
        return orderId;
    }

    private async Task<Guid> SubmitOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod) =>
        await SubmitOrderAsync(_client, fixture, idemKey, paymentMethod);

    private static BankTransferQrBuilder CreateBuilder(BankTransferOptions opts) =>
        new(Options.Create(opts));

    private static BankTransferOptions ValidOptions() => new()
    {
        Enabled = true,
        Provider = "VietQR",
        BankBin = "970000",
        BankName = "TEST BANK",
        AccountNumber = "0000000000",
        AccountName = "TEST COFFEE ACCOUNT",
        DescriptionTemplate = "KITS {Reference}",
        QrImageUrlTemplate = "https://img.vietqr.io/image/{bankBin}-{accountNumber}-compact2.png?amount={amount}&addInfo={memo}&accountName={accountName}"
    };

    private static Order SampleBankOrder(decimal total = 45000m) => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        TableCode = "T01",
        Status = OrderStatus.Submitted,
        PaymentMethod = OrderPaymentMethods.BankTransfer,
        TotalAmount = total,
        BillNumber = "AEDF18E3"
    };

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private static async Task<(Guid OrderId, string Token)> SubmitWithTokenAsync(
        HttpClient client,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod,
        int quantity = 1)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["paymentMethod"] = paymentMethod,
            ["items"] = new[] { new { menuItemId = fixture.MenuItemId, quantity, notes = (string?)null } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await client.SendAsync(req);
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
