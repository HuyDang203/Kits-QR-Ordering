using System.Net;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class AdminSalesReportsAccessTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private static DateTime BillDay => TestDay(50);

    private static DateTime TestDay(int offset) => new DateTime(2031, 1, 1).AddDays(offset);

    [Fact]
    public async Task Admin_can_access_admin_reports()
    {
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var res = await admin.GetAsync("/admin/reports");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Báo cáo bán hàng", html);
    }

    [Fact]
    public async Task Checkout_cannot_access_admin_reports()
    {
        var checkout = CreateNoRedirectClient();
        await LoginStaffAsync(checkout, "test-checkout-secret-16");

        var res = await checkout.GetAsync("/admin/reports");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Báo cáo bán hàng", html);
    }

    [Fact]
    public async Task Barista_cannot_access_admin_reports()
    {
        var barista = CreateNoRedirectClient();
        await LoginStaffAsync(barista, "test-barista-secret-16");

        var res = await barista.GetAsync("/admin/reports");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Báo cáo bán hàng", html);
    }

    [Fact]
    public async Task Admin_can_view_bill_detail_from_report()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(BillDay);

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 2));

        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = BillDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync($"/admin/reports?preset=custom&from={from}&to={from}&bill={orderId}");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("HÓA ĐƠN ĐIỆN TỬ", html);
        Assert.Contains("Coco", html);
        Assert.Contains("110", html);
    }

    [Fact]
    public async Task Non_admin_cannot_view_report_bill()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(BillDay.AddDays(1));

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var checkout = CreateNoRedirectClient();
        await LoginStaffAsync(checkout, "test-checkout-secret-16");

        var from = BillDay.AddDays(1).ToString("yyyy-MM-dd");
        var res = await checkout.GetAsync($"/admin/reports?preset=custom&from={from}&to={from}&bill={orderId}");
        AssertDenied(res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("HÓA ĐƠN ĐIỆN TỬ", html);
    }

    [Fact]
    public async Task Invalid_bill_id_keeps_report_page_with_inline_warning()
    {
        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var res = await admin.GetAsync("/admin/reports?preset=today&bill=00000000-0000-0000-0000-000000000001");
        res.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        Assert.Contains("Báo cáo bán hàng", html);
        Assert.Contains("Không tìm thấy bill trong khoảng báo cáo này.", html);
        Assert.DoesNotContain("HÓA ĐƠN ĐIỆN TỬ", html);
    }

    [Fact]
    public async Task Bill_outside_selected_range_shows_inline_warning()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var billDay = TestDay(51);
        var filterDay = TestDay(52);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(billDay);

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = filterDay.ToString("yyyy-MM-dd");
        var res = await admin.GetAsync($"/admin/reports?preset=custom&from={from}&to={from}&bill={orderId}");
        res.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        Assert.Contains("Báo cáo bán hàng", html);
        Assert.Contains("Không tìm thấy bill trong khoảng báo cáo này.", html);
        Assert.DoesNotContain("HÓA ĐƠN ĐIỆN TỬ", html);
    }

    [Fact]
    public async Task View_bill_link_uses_order_id_and_preserves_custom_range_filters()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var billDay = TestDay(55);
        var rangeFrom = TestDay(54);
        var rangeTo = TestDay(56);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(billDay);

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = rangeFrom.ToString("yyyy-MM-dd");
        var to = rangeTo.ToString("yyyy-MM-dd");
        var listRes = await admin.GetAsync($"/admin/reports?preset=custom&from={from}&to={to}");
        listRes.EnsureSuccessStatusCode();
        var listHtml = await listRes.Content.ReadAsStringAsync();

        var linkMatch = Regex.Match(
            listHtml,
            "href=\"(?<href>/admin/reports[^\"]+)\"[^>]*>\\s*Xem bill\\s*</a>",
            RegexOptions.IgnoreCase);
        Assert.True(linkMatch.Success, "Expected a Xem bill link in the paid bills table.");

        var href = WebUtility.HtmlDecode(linkMatch.Groups["href"].Value);
        Assert.Contains($"bill={orderId:D}", href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preset=custom", href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"from={from}", href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"to={to}", href, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(href, @"bill=A[0-9A-F]{8}(?:&|$)", RegexOptions.IgnoreCase));

        var detailRes = await admin.GetAsync(href.Split('#')[0]);
        detailRes.EnsureSuccessStatusCode();
        var detailHtml = await detailRes.Content.ReadAsStringAsync();
        Assert.Contains("HÓA ĐƠN ĐIỆN TỬ", detailHtml);
        Assert.Contains("Coco", detailHtml);
    }

    [Fact]
    public async Task Close_bill_link_preserves_filters_and_removes_bill_param()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var billDay = TestDay(57);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(billDay);

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var admin = CreateNoRedirectClient();
        await LoginStaffAsync(admin, "test-staff-secret-16");

        var from = billDay.ToString("yyyy-MM-dd");
        var openRes = await admin.GetAsync($"/admin/reports?preset=custom&from={from}&to={from}&bill={orderId}");
        openRes.EnsureSuccessStatusCode();
        var openHtml = await openRes.Content.ReadAsStringAsync();

        var closeMatch = Regex.Match(
            openHtml,
            "href=\"(?<href>/admin/reports[^\"]+)\"[^>]*>\\s*Đóng bill\\s*</a>",
            RegexOptions.IgnoreCase);
        Assert.True(closeMatch.Success, "Expected a Đóng bill link when bill detail is open.");

        var closeHref = WebUtility.HtmlDecode(closeMatch.Groups["href"].Value);
        Assert.Contains("preset=custom", closeHref, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"from={from}", closeHref, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"to={from}", closeHref, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bill=", closeHref, StringComparison.OrdinalIgnoreCase);

        var closeRes = await admin.GetAsync(closeHref);
        closeRes.EnsureSuccessStatusCode();
        var closeHtml = await closeRes.Content.ReadAsStringAsync();
        Assert.Contains("Báo cáo bán hàng", closeHtml);
        Assert.DoesNotContain("id=\"bill-detail\"", closeHtml);
    }

    [Fact]
    public async Task Paid_bill_row_model_includes_order_id()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ctx = await SalesReportTestSeedHelper.SeedMenuAndTableAsync(db);
        var billDay = TestDay(58);
        var paidAt = SalesReportTestSeedHelper.PaidAtForLocalDate(billDay);

        var orderId = await SalesReportTestSeedHelper.InsertPaidOrderAsync(
            db, ctx, paidAt, OrderStatus.Paid,
            (ctx.MenuItemAId, "Coco Bơ", 55000m, 1));

        var report = await AdminSalesReportQuery.LoadAsync(db, billDay, billDay);
        var row = Assert.Single(report.PaidBills);
        Assert.Equal(orderId, row.OrderId);
        Assert.NotEqual(row.BillNumber, row.OrderId.ToString());
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
        var token = tokenMatch.Groups["v"].Value;
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
