using System.Net;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffShiftCloseUxTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Shift_close_page_renders_kpis_near_top()
    {
        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        var kpiPos = html.IndexOf("staff-shift-close__kpis", StringComparison.Ordinal);
        var lastPos = html.IndexOf("staff-shift-close__last", StringComparison.Ordinal);
        var empPos = html.IndexOf("shift-emp-heading", StringComparison.Ordinal);
        Assert.True(kpiPos >= 0);
        Assert.True(empPos < 0 || kpiPos < empPos);
        Assert.True(lastPos < 0 || kpiPos < lastPos);
    }

    [Fact]
    public async Task Shift_close_page_contains_compact_current_shift_bar()
    {
        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("staff-shift-close__shift-bar", html);
        Assert.Contains("Ca hiện tại", html);
    }

    [Fact]
    public async Task Shift_close_page_contains_employee_breakdown_section()
    {
        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("Nhân viên xác nhận", html);
        Assert.Contains("shift-emp-heading", html);
    }

    [Fact]
    public async Task Shift_close_page_contains_bill_list_section()
    {
        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("Danh sách bill trong ca", html);
        Assert.Contains("shift-bills-heading", html);
    }

    [Fact]
    public async Task Shift_close_page_contains_last_closed_accordion()
    {
        var html = await GetShiftCloseHtmlAsync("test-staff-secret-16");
        Assert.Contains("staff-shift-close__last", html);
        Assert.Contains("Ca đã kết gần nhất", html);
    }

    [Fact]
    public async Task Zero_paid_bills_disables_close_button()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("Chưa có bill thanh toán trong ca này", html);
        Assert.Contains("staff-shift-close-btn", html);
        Assert.Contains("disabled", html);
    }

    [Fact]
    public async Task Non_zero_paid_bills_enables_close_button()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        db.Orders.Add(PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), 65000m, "Nguyễn A"));
        await db.SaveChangesAsync();

        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("staff-shift-close-btn", html);
        Assert.DoesNotContain("id=\"staff-shift-close-btn\" disabled", html.Replace(" ", ""));
        Assert.Contains("staff-shift-close__sticky", html);
    }

    [Fact]
    public async Task Copy_summary_includes_payment_split_and_employees()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        db.Orders.Add(PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), 70000m, "Nguyễn A"));
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        var text = svc.BuildCopyText(preview);
        Assert.Contains("KẾT CA ANNAP", text);
        Assert.Contains("Người kết ca:", text);
        Assert.Contains("Tiền mặt:", text);
        Assert.Contains("Thẻ:", text);
        Assert.Contains("Chuyển khoản:", text);
        Assert.Contains("Theo nhân viên:", text);
        Assert.Contains("Nguyễn A", text);
    }

    [Fact]
    public async Task Shift_close_page_has_mobile_employee_cards_markup()
    {
        var html = await GetShiftCloseHtmlAsync("test-checkout-secret-16");
        Assert.Contains("staff-shift-close__emp-cards", html);
        Assert.Contains("staff-shift-close__emp-card", html);
    }

    [Fact]
    public async Task Barista_still_cannot_access_shift_close()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        await LoginSharedAsync(client, "test-barista-secret-16");
        var res = await client.GetAsync("/staff/shift-close");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private async Task<string> GetShiftCloseHtmlAsync(string password)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        await LoginSharedAsync(client, password);
        var res = await client.GetAsync("/staff/shift-close");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
    }

    private static Order PaidOrder(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        DateTimeOffset paidAt,
        decimal total,
        string confirmedBy) => new()
    {
        VenueTableId = fixture.VenueTableId,
        TableCode = "T01",
        Status = OrderStatus.Paid,
        PaymentMethod = OrderPaymentMethods.Cash,
        PaymentConfirmedBy = confirmedBy,
        PaidAtUtc = paidAt,
        TotalAmount = total,
        BillNumber = $"A{Guid.NewGuid():N}"[..9].ToUpperInvariant(),
        CreatedAtUtc = paidAt.AddMinutes(-5)
    };

    private static System.Security.Claims.ClaimsPrincipal TestCheckoutPrincipal() =>
        new(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "StaffCheckout")],
            "test"));

    private static async Task LoginSharedAsync(HttpClient client, string password)
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
}
