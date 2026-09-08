using System.Net;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class AdminPaymentConfirmationsAccessTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string RawPayloadMarker = "SECRET_RAW_PAYLOAD_MARKER_4A1";
    private static DateTime TestDay => AnnapBusinessTime.TodayLocal;

    [Fact]
    public async Task Admin_can_access_payment_confirmations_page()
    {
        await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var res = await admin.GetAsync("/admin/payments");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Đối soát chuyển khoản", html);
        Assert.Contains("Tổng webhook nhận được", html);
    }

    [Fact]
    public async Task Checkout_cannot_access_payment_confirmations_page()
    {
        var checkout = CreateNoRedirectClient();
        await LoginStaffAsync(checkout, "test-checkout-secret-16");

        var res = await checkout.GetAsync("/admin/payments");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Đối soát chuyển khoản", html);
    }

    [Fact]
    public async Task Barista_cannot_access_payment_confirmations_page()
    {
        var barista = CreateNoRedirectClient();
        await LoginStaffAsync(barista, "test-barista-secret-16");

        var res = await barista.GetAsync("/admin/payments");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Đối soát chuyển khoản", html);
    }

    [Fact]
    public async Task Anonymous_cannot_access_payment_confirmations_page()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/admin/payments");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Đối soát chuyển khoản", html);
    }

    [Fact]
    public async Task Default_page_lists_recent_confirmations_in_last_7_days()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var res = await admin.GetAsync("/admin/payments");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains(seed.MatchedTxnId, html);
        Assert.Contains(seed.UnmatchedMemo, html);
        Assert.Contains("Đã khớp", html);
        Assert.Contains("Chưa khớp", html);
    }

    [Fact]
    public async Task Status_filter_limits_rows()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync(
            $"/admin/payments?preset=custom&from={from}&to={from}&status={PaymentConfirmationMatchStatus.Duplicate}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains(seed.DuplicateTxnId, html);
        Assert.DoesNotContain(seed.MatchedTxnId, html);
        Assert.Contains("Trùng giao dịch", html);
    }

    [Fact]
    public async Task Provider_filter_limits_rows()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync(
            $"/admin/payments?preset=custom&from={from}&to={from}&provider={Uri.EscapeDataString(seed.AltProvider)}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains(seed.AltProviderTxnId, html);
        Assert.DoesNotContain(seed.MatchedTxnId, html);
    }

    [Fact]
    public async Task Memo_search_finds_confirmation()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync(
            $"/admin/payments?preset=custom&from={from}&to={from}&q={Uri.EscapeDataString(seed.UnmatchedMemo)}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains(seed.UnmatchedTxnId, html);
        Assert.DoesNotContain(seed.MatchedTxnId, html);
    }

    [Fact]
    public async Task Bill_search_finds_matched_confirmation()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync(
            $"/admin/payments?preset=custom&from={from}&to={from}&q={Uri.EscapeDataString(seed.BillNumber)}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains(seed.MatchedTxnId, html);
        Assert.Contains(seed.BillNumber, html);
        Assert.Contains("Xem bill", html);
    }

    [Fact]
    public async Task Detail_panel_shows_raw_payload_for_admin_only()
    {
        var seed = await SeedConfirmationsAsync();
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync(
            $"/admin/payments?preset=custom&from={from}&to={from}&id={seed.RawPayloadConfirmationId:D}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Raw payload (chỉ Admin)", html);
        Assert.Contains(RawPayloadMarker, html);

        var checkout = CreateNoRedirectClient();
        await LoginStaffAsync(checkout, "test-checkout-secret-16");
        var staffRes = await checkout.GetAsync("/Staff/Orders");
        staffRes.EnsureSuccessStatusCode();
        var staffHtml = await staffRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(RawPayloadMarker, staffHtml);
    }

    [Fact]
    public async Task Empty_state_renders_cleanly()
    {
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.AddYears(-5).ToString("yyyy-MM-dd");
        var to = TestDay.AddYears(-5).AddDays(1).ToString("yyyy-MM-dd");
        var res = await admin.GetAsync($"/admin/payments?preset=custom&from={from}&to={to}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Chưa có webhook nào trong khoảng này.", html);
        Assert.Contains("Tổng webhook nhận được", html);
    }

    [Fact]
    public async Task Pagination_does_not_crash_with_many_rows()
    {
        await SeedManyConfirmationsAsync(55);
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = TestDay.ToString("yyyy-MM-dd");
        var page1 = await admin.GetAsync($"/admin/payments?preset=custom&from={from}&to={from}");
        page1.EnsureSuccessStatusCode();
        var html1 = await page1.Content.ReadAsStringAsync();
        Assert.Contains("Trang 1 /", html1);

        var page2 = await admin.GetAsync($"/admin/payments?preset=custom&from={from}&to={from}&page=2");
        page2.EnsureSuccessStatusCode();
        var html2 = await page2.Content.ReadAsStringAsync();
        Assert.Contains("Trang 2 /", html2);
        Assert.Contains("Sau →", html1);
        Assert.Contains("← Trước", html2);
    }

    private async Task<PaymentConfirmationSeed> SeedConfirmationsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(TestDay);
        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));
        var billNumber = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.BillNumber)
            .SingleAsync();

        var receivedAt = SalesReportTestSeedHelper.PaidAtForLocalDate(TestDay).AddHours(2);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var matchedId = Guid.NewGuid();
        var unmatchedId = Guid.NewGuid();
        var duplicateId = Guid.NewGuid();
        var amountMismatchId = Guid.NewGuid();
        var altProviderId = Guid.NewGuid();
        var rawPayloadId = Guid.NewGuid();
        var matchedTxn = $"pc-txn-matched-{suffix}";
        var unmatchedTxn = $"pc-txn-unmatched-{suffix}";
        var duplicateTxn = $"pc-txn-dup-{suffix}";
        var amountMismatchTxn = $"pc-txn-amt-{suffix}";
        var altProviderTxn = $"pc-txn-sepay-{suffix}";
        var unmatchedMemo = $"KITS UNMATCHED-4A1-{suffix}";
        var altProvider = $"sepay-test-{suffix}";
        var rawTxn = $"pc-txn-raw-{suffix}";

        db.PaymentConfirmations.AddRange(
            new PaymentConfirmation
            {
                Id = matchedId,
                Provider = "dev",
                ProviderTransactionId = matchedTxn,
                ReceivedAtUtc = receivedAt,
                Amount = 55000m,
                Memo = $"KITS {billNumber}",
                MatchStatus = PaymentConfirmationMatchStatus.Matched,
                MatchedOrderId = orderId,
                Notes = "Auto-matched"
            },
            new PaymentConfirmation
            {
                Id = unmatchedId,
                Provider = "dev",
                ProviderTransactionId = unmatchedTxn,
                ReceivedAtUtc = receivedAt.AddMinutes(1),
                Amount = 55000m,
                Memo = unmatchedMemo,
                MatchStatus = PaymentConfirmationMatchStatus.Unmatched,
                Notes = "No order found"
            },
            new PaymentConfirmation
            {
                Id = duplicateId,
                Provider = "dev",
                ProviderTransactionId = duplicateTxn,
                ReceivedAtUtc = receivedAt.AddMinutes(2),
                Amount = 55000m,
                Memo = $"KITS {billNumber}",
                MatchStatus = PaymentConfirmationMatchStatus.Duplicate,
                Notes = "Duplicate provider transaction"
            },
            new PaymentConfirmation
            {
                Id = amountMismatchId,
                Provider = "dev",
                ProviderTransactionId = amountMismatchTxn,
                ReceivedAtUtc = receivedAt.AddMinutes(3),
                Amount = 1m,
                Memo = $"KITS {billNumber}",
                MatchStatus = PaymentConfirmationMatchStatus.AmountMismatch,
                Notes = "Amount mismatch"
            },
            new PaymentConfirmation
            {
                Id = altProviderId,
                Provider = altProvider,
                ProviderTransactionId = altProviderTxn,
                ReceivedAtUtc = receivedAt.AddMinutes(4),
                Amount = 1000m,
                Memo = "ALT PROVIDER",
                MatchStatus = PaymentConfirmationMatchStatus.Ignored
            },
            new PaymentConfirmation
            {
                Id = rawPayloadId,
                Provider = "dev",
                ProviderTransactionId = rawTxn,
                ReceivedAtUtc = receivedAt.AddMinutes(5),
                Amount = 1000m,
                Memo = "RAW TEST",
                MatchStatus = PaymentConfirmationMatchStatus.Unmatched,
                AccountNumber = "1234567890",
                BankCode = "VCB",
                RawPayloadJson = $"{{\"marker\":\"{RawPayloadMarker}\"}}"
            });
        await db.SaveChangesAsync();

        return new PaymentConfirmationSeed(
            matchedTxn,
            unmatchedTxn,
            duplicateTxn,
            amountMismatchTxn,
            altProviderTxn,
            unmatchedMemo,
            altProvider,
            billNumber!,
            rawPayloadId);
    }

    private async Task SeedManyConfirmationsAsync(int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receivedAt = SalesReportTestSeedHelper.PaidAtForLocalDate(TestDay).AddHours(1);
        var batch = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < count; i++)
        {
            db.PaymentConfirmations.Add(new PaymentConfirmation
            {
                Provider = "dev",
                ProviderTransactionId = $"pc-bulk-{batch}-{i:D3}",
                ReceivedAtUtc = receivedAt.AddMinutes(i),
                Amount = 1000m + i,
                Memo = $"BULK {batch} {i}",
                MatchStatus = PaymentConfirmationMatchStatus.Unmatched
            });
        }

        await db.SaveChangesAsync();
    }

    private HttpClient CreateNoRedirectClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static void AssertDenied(HttpResponseMessage res)
    {
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    private static async Task LoginStaffAsync(HttpClient client, string password)
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
            ["Password"] = password,
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private sealed record PaymentConfirmationSeed(
        string MatchedTxnId,
        string UnmatchedTxnId,
        string DuplicateTxnId,
        string AmountMismatchTxnId,
        string AltProviderTxnId,
        string UnmatchedMemo,
        string AltProvider,
        string BillNumber,
        Guid RawPayloadConfirmationId);
}
