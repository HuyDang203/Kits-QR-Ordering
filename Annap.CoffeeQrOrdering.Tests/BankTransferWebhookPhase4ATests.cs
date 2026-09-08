using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Extensions;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class BankTransferWebhookPhase4ATests(BankTransferWebhookPostgresWebApplicationFactory factory)
    : IClassFixture<BankTransferWebhookPostgresWebApplicationFactory>
{
    private const string WebhookSecret = "test-webhook-secret-16";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PaymentConfirmation_table_is_available()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = await db.PaymentConfirmations.CountAsync();
    }

    [Fact]
    public async Task Webhook_matching_memo_and_amount_auto_marks_order_paid()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, token, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-match-1");

        var res = await PostDevWebhookAsync(memo, amount, "wh-txn-match-1");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("matched", body.GetProperty("status").GetString());
        Assert.Equal(orderId, body.GetProperty("orderId").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAtUtc);
        Assert.Equal("bank-webhook:dev", order.PaymentConfirmedBy);

        var confirmation = await db.PaymentConfirmations.AsNoTracking()
            .SingleAsync(c => c.ProviderTransactionId == "wh-txn-match-1");
        Assert.Equal(PaymentConfirmationMatchStatus.Matched, confirmation.MatchStatus);
        Assert.Equal(orderId, confirmation.MatchedOrderId);
        Assert.Equal(memo, confirmation.Memo);
        Assert.Equal(amount, confirmation.Amount);

        var track = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.False(track.GetProperty("pendingPayment").GetBoolean());
        Assert.True(track.GetProperty("showBill").GetBoolean());
    }

    [Fact]
    public async Task Webhook_amount_mismatch_does_not_mark_paid()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-amt-1");

        var res = await PostDevWebhookAsync(memo, amount + 1, "wh-txn-amt-1");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("amount_mismatch", body.GetProperty("status").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Webhook_wrong_memo_does_not_mark_paid()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, _, amount) = await SubmitBankOrderAsync(fixture, "wh-memo-1");

        var res = await PostDevWebhookAsync("ANNAP WRONGREF", amount, "wh-txn-memo-1");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unmatched", body.GetProperty("status").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(OrderStatus.Submitted, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Webhook_does_not_match_cash_order()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "wh-cash-1", OrderPaymentMethods.CashOrCardAtCounter);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        var memo = $"KITS {OrderBillHelper.EnsureBillNumber(order)}";

        var res = await PostDevWebhookAsync(memo, (long)order.TotalAmount, "wh-txn-cash-1");
        Assert.Equal("unmatched", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task Webhook_already_paid_order_is_not_marked_again()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-paid-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var res = await PostDevWebhookAsync(memo, amount, "wh-txn-paid-second");
        Assert.Equal("unmatched", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Webhook_duplicate_provider_transaction_is_idempotent()
    {
        var fixture = await SeedFixtureAsync();
        var (_, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-dup-1");

        var first = await PostDevWebhookAsync(memo, amount, "wh-txn-dup-1");
        Assert.Equal("matched", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var second = await PostDevWebhookAsync(memo, amount, "wh-txn-dup-1");
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("duplicate", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Webhook_multiple_possible_matches_do_not_auto_confirm()
    {
        var fixture = await SeedFixtureAsync();
        var (_, _, memo1, amount) = await SubmitBankOrderAsync(fixture, "wh-multi-a");
        var (_, _, memo2, _) = await SubmitBankOrderAsync(fixture, "wh-multi-b");
        var combinedMemo = $"{memo1} {memo2}";

        var res = await PostDevWebhookAsync(combinedMemo, amount, "wh-txn-multi");
        Assert.Equal("unmatched", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public void Memo_matching_is_trim_and_case_insensitive()
    {
        Assert.True(BankTransferMemoMatcher.MemoMatches("  kits  aedf18e3  ", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3 EXTRA", "KITS AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("kits wrong", "KITS AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_legacy_annap_to_kits_compatibility()
    {
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("  kits  aedf18e3  ", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3 EXTRA", "KITS AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_kits_expected_legacy_annap_incoming()
    {
        Assert.True(BankTransferMemoMatcher.MemoMatches("ANNAP AEDF18E3", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("  annap  aedf18e3  ", "KITS AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("ANNAP AEDF18E3 EXTRA", "KITS AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_annap_expected_kits_incoming()
    {
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3", "ANNAP AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("KITS AEDF18E3 EXTRA", "ANNAP AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_cross_prefix_wrong_reference_no_match()
    {
        Assert.False(BankTransferMemoMatcher.MemoMatches("ANNAP WRONG999", "KITS AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("KITS WRONG999", "KITS AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("KITS WRONG999", "ANNAP AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("ANNAP WRONG999", "ANNAP AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_unrelated_prefix_same_reference_no_match()
    {
        Assert.False(BankTransferMemoMatcher.MemoMatches("OTHER AEDF18E3", "KITS AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("OTHER AEDF18E3", "ANNAP AEDF18E3"));
    }

    [Fact]
    public void Memo_matching_legacy_annap_annap_still_works()
    {
        Assert.True(BankTransferMemoMatcher.MemoMatches("  annap  aedf18e3  ", "ANNAP AEDF18E3"));
        Assert.True(BankTransferMemoMatcher.MemoMatches("ANNAP AEDF18E3 EXTRA", "ANNAP AEDF18E3"));
        Assert.False(BankTransferMemoMatcher.MemoMatches("annap wrong", "ANNAP AEDF18E3"));
    }

    [Fact]
    public async Task Auto_confirm_preserves_bill_number()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-bill-1");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).BillNumber;

        await PostDevWebhookAsync(memo, amount, "wh-txn-bill-1");

        var after = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(before, after.BillNumber);
    }

    [Fact]
    public async Task Auto_confirmed_order_moves_to_paid_column_on_staff_board()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-board-1");
        await PostDevWebhookAsync(memo, amount, "wh-txn-board-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.DoesNotContain(
            board.GetProperty("submitted").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Contains(
            board.GetProperty("paid").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Manual_mark_paid_still_works_after_unmatched_webhook()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, _, amount) = await SubmitBankOrderAsync(fixture, "wh-manual-1");
        await PostDevWebhookAsync("ANNAP NOMATCH", amount, "wh-txn-manual");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
    }

    [Fact]
    public async Task Dev_webhook_returns_not_found_when_disabled()
    {
        await using var disabledFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BankTransfer:Webhook:DevWebhookEnabled"] = "false"
                });
            });
        });
        var res = await disabledFactory.CreateClient().PostAsJsonAsync("/api/webhooks/bank-transfer/dev", new
        {
            provider = "dev",
            transactionId = "disabled-txn-1",
            amount = 1000m,
            memo = "KITS TEST"
        });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData("Development", null, null, true)]
    [InlineData("Production", "test-webhook-secret-16", null, false)]
    [InlineData("Production", "test-webhook-secret-16", "wrong-secret", false)]
    [InlineData("Production", "test-webhook-secret-16", "test-webhook-secret-16", false)]
    public void Dev_webhook_authorization_is_development_only(
        string environmentName,
        string? configuredSecret,
        string? providedSecret,
        bool expected)
    {
        var env = new FakeHostEnvironment(environmentName);
        var webhook = new BankTransferWebhookOptions { Secret = configuredSecret ?? "" };
        var http = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(providedSecret))
            http.Request.Headers[BankTransferWebhookEndpoints.WebhookSecretHeader] = providedSecret;
        Assert.Equal(expected, BankTransferWebhookEndpoints.IsWebhookAuthorized(http, env, webhook));
    }

    [Fact]
    public async Task Webhook_does_not_require_guest_token()
    {
        var fixture = await SeedFixtureAsync();
        var (_, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-no-token");
        var res = await PostDevWebhookAsync(memo, amount, "wh-txn-no-token");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Webhook_only_marks_paid_does_not_complete_order()
    {
        var fixture = await SeedFixtureAsync();
        var (orderId, _, memo, amount) = await SubmitBankOrderAsync(fixture, "wh-no-complete");
        await PostDevWebhookAsync(memo, amount, "wh-txn-no-complete");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.CompletedAtUtc);
    }

    private async Task<(Guid OrderId, string Token, string Memo, long Amount)> SubmitBankOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey)
    {
        var (orderId, token) = await SubmitWithTokenAsync(fixture, idemKey, OrderPaymentMethods.BankTransfer);
        var qr = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        return (orderId, token, qr.GetProperty("memo").GetString()!, qr.GetProperty("amount").GetInt64());
    }

    private Task<HttpResponseMessage> PostDevWebhookAsync(string memo, long amount, string transactionId) =>
        _client.PostAsJsonAsync("/api/webhooks/bank-transfer/dev", new
        {
            provider = "dev",
            transactionId,
            amount,
            memo,
            receivedAtUtc = DateTimeOffset.UtcNow
        });

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<Guid> SubmitOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod) =>
        (await SubmitWithTokenAsync(fixture, idemKey, paymentMethod)).OrderId;

    private async Task<(Guid OrderId, string Token)> SubmitWithTokenAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod) =>
        await SubmitWithTokenAsync(_client, fixture, idemKey, paymentMethod);

    private static async Task<(Guid OrderId, string Token)> SubmitWithTokenAsync(
        HttpClient client,
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

    private sealed class FakeHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Annap.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
