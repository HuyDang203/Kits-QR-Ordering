using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffShiftClosePhase1Tests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string EmployeeUsername = "shift-qa-employee";
    private const string EmployeeDisplayName = "Nguyễn Văn A";
    private const string EmployeePassword = "Test12345!";

    [Fact]
    public async Task Admin_can_access_shift_close_page()
    {
        var admin = CreateNoRedirectClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        var res = await admin.GetAsync("/staff/shift-close");
        res.EnsureSuccessStatusCode();
        Assert.Contains("Kết ca", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Shared_checkout_can_access_shift_close_page()
    {
        var checkout = CreateNoRedirectClient();
        await LoginSharedAsync(checkout, "test-checkout-secret-16");
        var res = await checkout.GetAsync("/staff/shift-close");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Employee_checkout_can_access_shift_close_page()
    {
        await EnsureEmployeeAccountAsync();
        var employee = CreateNoRedirectClient();
        await LoginEmployeeAsync(employee);
        var res = await employee.GetAsync("/staff/shift-close");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Barista_cannot_access_shift_close_page()
    {
        var barista = CreateNoRedirectClient();
        await LoginSharedAsync(barista, "test-barista-secret-16");
        var res = await barista.GetAsync("/staff/shift-close");
        AssertDenied(res);
    }

    [Fact]
    public async Task Anonymous_cannot_access_shift_close_page()
    {
        var res = await factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/staff/shift-close");
        AssertDenied(res);
    }

    [Fact]
    public async Task Employee_sees_ket_ca_link_on_orders_page()
    {
        await EnsureEmployeeAccountAsync();
        var employee = CreateNoRedirectClient();
        await LoginEmployeeAsync(employee);
        var html = await (await employee.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("Kết ca", html);
        Assert.Contains("/staff/shift-close", html);
    }

    [Fact]
    public async Task Barista_does_not_see_ket_ca_link_on_orders_page()
    {
        var barista = CreateNoRedirectClient();
        await LoginSharedAsync(barista, "test-barista-secret-16");
        var html = await (await barista.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("/staff/shift-close", html);
    }

    [Fact]
    public async Task Preview_includes_only_paid_orders_in_window()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();

        var windowStart = await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var inWindow = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, EmployeeDisplayName);
        var beforeWindow = PaidOrder(fixture, windowStart.AddMinutes(-1), OrderPaymentMethods.CashOrCardAtCounter, 50000m, "Old");
        var unpaid = new Order
        {
            VenueTableId = fixture.VenueTableId,
            TableCode = "T99",
            Status = OrderStatus.Submitted,
            PaymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            TotalAmount = 40000m,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Orders.AddRange(inWindow, beforeWindow, unpaid);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());

        Assert.Single(preview.Bills);
        Assert.Equal(inWindow.Id, preview.Bills[0].OrderId);
        Assert.Equal(65000m, preview.TotalGrossAmount);
    }

    [Fact]
    public async Task Preview_splits_cash_card_and_bank_totals()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var cash = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.Cash, 70000m, "A");
        var card = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.Card, 55000m, "A");
        var bank = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.BankTransfer, 80000m, "A");
        db.Orders.AddRange(cash, card, bank);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        Assert.Equal(3, preview.TotalOrders);
        Assert.Equal(70000m, preview.CashAmount);
        Assert.Equal(55000m, preview.CardAmount);
        Assert.Equal(80000m, preview.BankTransferAmount);
        Assert.Equal(205000m, preview.TotalGrossAmount);
    }

    [Fact]
    public async Task Preview_groups_employee_by_account_id()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var accounts = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var username = $"shift-group-emp-{Guid.NewGuid():N}"[..20];
        var (account, _) = await accounts.CreateAsync(
            new StaffAccountCreateRequest(username, EmployeeDisplayName, EmployeePassword),
            "test");
        Assert.NotNull(account);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, EmployeeDisplayName);
        order.PaymentConfirmedByAccountId = account!.Id;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        Assert.Single(preview.Employees);
        Assert.Equal(EmployeeDisplayName, preview.Employees[0].DisplayName);
        Assert.Equal(account.Id, preview.Employees[0].AccountId);
    }

    [Fact]
    public async Task Closing_creates_shift_close_row_with_snapshot()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, EmployeeDisplayName);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await svc.CloseShiftAsync(TestCheckoutPrincipal());
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Entity);

        var row = await db.ShiftCloses.AsNoTracking()
            .Where(s => s.ClosedBy != "test-isolation")
            .OrderByDescending(s => s.ClosedAtUtc)
            .FirstAsync();
        Assert.Equal(1, row.TotalOrders);
        Assert.Equal(65000m, row.TotalGrossAmount);
        Assert.Contains("employees", row.SnapshotJson);
        Assert.Contains("bills", row.SnapshotJson);
    }

    [Fact]
    public async Task After_close_new_preview_starts_after_close_time()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, "A");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        Assert.True((await svc.CloseShiftAsync(TestCheckoutPrincipal())).Success);
        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        Assert.Equal(0, preview.TotalOrders);
        Assert.False(preview.CanClose);
    }

    [Fact]
    public async Task Close_with_zero_orders_fails_validation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var realCloseCount = await db.ShiftCloses.CountAsync(s => s.ClosedBy != "test-isolation");

        var result = await svc.CloseShiftAsync(TestCheckoutPrincipal());
        Assert.False(result.Success);
        Assert.Contains("Chưa có bill", result.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.Equal(realCloseCount, await db.ShiftCloses.CountAsync(s => s.ClosedBy != "test-isolation"));
    }

    [Fact]
    public async Task Preview_handles_unknown_payment_method()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), "legacy-cash", 55000m, "A");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        Assert.Equal(55000m, preview.UnknownPaymentAmount);
        Assert.Equal(1, preview.UnknownPaymentOrders);
    }

    [Fact]
    public async Task Preview_groups_employee_by_confirmed_by_text_when_no_account()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, "Trần B");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var preview = await svc.BuildPreviewAsync(TestCheckoutPrincipal());
        Assert.Single(preview.Employees);
        Assert.Equal("Trần B", preview.Employees[0].DisplayName);
    }

    [Fact]
    public async Task Double_close_does_not_duplicate_same_window()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var realCloseCount = await db.ShiftCloses.CountAsync(s => s.ClosedBy != "test-isolation");

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var order = PaidOrder(fixture, ShiftCloseTestHelper.PaidAfterBoundary(), OrderPaymentMethods.CashOrCardAtCounter, 65000m, "A");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        Assert.True((await svc.CloseShiftAsync(TestCheckoutPrincipal())).Success);
        var second = await svc.CloseShiftAsync(TestCheckoutPrincipal());
        Assert.False(second.Success);
        Assert.Equal(realCloseCount + 1, await db.ShiftCloses.CountAsync(s => s.ClosedBy != "test-isolation"));
    }

    [Fact]
    public async Task Staff_orders_board_still_loads_for_checkout()
    {
        var checkout = CreateNoRedirectClient();
        await LoginSharedAsync(checkout, "test-checkout-secret-16");
        var res = await checkout.GetAsync("/staff/orders");
        res.EnsureSuccessStatusCode();
        var html = System.Net.WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        Assert.Contains("Sàn phục vụ", html);
    }

    [Fact]
    public async Task Bill_list_contains_required_fields()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        var boundary = await ShiftCloseTestHelper.IsolateWindowAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var paidAt = ShiftCloseTestHelper.PaidAfterBoundary();
        var order = PaidOrder(fixture, paidAt, OrderPaymentMethods.BankTransfer, 80000m, "Nguyễn Văn A");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var bill = Assert.Single((await svc.BuildPreviewAsync(TestCheckoutPrincipal())).Bills);
        Assert.False(string.IsNullOrWhiteSpace(bill.BillNumber));
        Assert.Equal(paidAt.ToUnixTimeSeconds(), bill.PaidAtUtc.ToUnixTimeSeconds());
        Assert.Equal(80000m, bill.TotalAmount);
        Assert.Equal("Nguyễn Văn A", bill.ConfirmedBy);
        Assert.Equal("Chuyển khoản", bill.PaymentMethodLabelVi);
    }

    [Fact]
    public async Task Employee_mark_paid_attribution_still_works_after_shift_close_feature()
    {
        await EnsureEmployeeAccountAsync();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "shift-regression-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(EmployeeDisplayName, order.PaymentConfirmedBy);
    }

    private static Order PaidOrder(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        DateTimeOffset paidAt,
        string paymentMethod,
        decimal total,
        string confirmedBy) => new()
    {
        VenueTableId = fixture.VenueTableId,
        TableCode = "T01",
        Status = OrderStatus.Paid,
        PaymentMethod = paymentMethod,
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

    private async Task EnsureEmployeeAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(EmployeeUsername, EmployeePassword) is not null)
            return;
        var (_, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(EmployeeUsername, EmployeeDisplayName, EmployeePassword),
            "test");
        Assert.Null(error);
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<Guid> SubmitOrderAsync(OrderTestSeedHelper.OrderSubmitFixture fixture, string idemKey)
    {
        var client = factory.CreateClient();
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
            paymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private HttpClient CreateNoRedirectClient()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private HttpClient CreateClientWithPartition()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private static void AssertDenied(HttpResponseMessage res)
    {
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

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

    private static async Task LoginEmployeeAsync(HttpClient client)
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
            ["UserName"] = EmployeeUsername,
            ["Password"] = EmployeePassword,
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
