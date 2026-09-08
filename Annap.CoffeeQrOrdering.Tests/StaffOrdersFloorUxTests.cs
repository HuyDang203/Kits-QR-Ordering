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
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffOrdersFloorUxTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string EmployeeUsername = "floor-ux-employee";
    private const string EmployeeDisplayName = "Nguyễn Văn A";
    private const string EmployeePassword = "Test12345!";

    [Fact]
    public async Task Orders_page_shows_employee_display_name()
    {
        await EnsureEmployeeAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client);

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync());
        Assert.Contains("Đang đăng nhập: Nguyễn Văn A", html);
        Assert.Contains("@floor-ux-employee", html);
    }

    [Fact]
    public async Task Orders_page_shows_switch_employee_action()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-staff-secret-16");

        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("Đổi nhân viên", html);
    }

    [Fact]
    public async Task Employee_checkout_sees_ket_ca_on_orders_page()
    {
        await EnsureEmployeeAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client);

        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("Kết ca", html);
        Assert.Contains("/staff/shift-close", html);
    }

    [Fact]
    public async Task Barista_does_not_see_ket_ca_on_orders_page()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-barista-secret-16");

        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("/staff/shift-close", html);
    }

    [Fact]
    public async Task Employee_checkout_does_not_see_admin_link()
    {
        await EnsureEmployeeAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client);

        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("/admin", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Quản lý</a>", html);
    }

    [Fact]
    public async Task Admin_sees_admin_link_on_orders_page()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-staff-secret-16");

        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("Quản lý", html);
        Assert.Contains("/admin", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Switch_employee_signs_out_and_redirects_to_login()
    {
        var client = CreateNoRedirectClient();
        await LoginSharedAsync(client, "test-staff-secret-16");

        var get = await client.GetAsync("/staff/orders");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success);

        var logout = await client.PostAsync(
            "/Staff/Logout?returnToLogin=true&returnUrl=%2Fstaff%2Forders",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
            }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var location = logout.Headers.Location?.ToString() ?? "";
        Assert.Contains("/staff/login", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnUrl", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_page_contains_employee_account_copy()
    {
        var html = await (await factory.CreateClient().GetAsync("/staff/login")).Content.ReadAsStringAsync();
        Assert.Contains("Nhân viên dùng tên đăng nhập", html);
        Assert.Contains("Tên đăng nhập", html);
        Assert.Contains("Mật khẩu", html);
        Assert.Contains("có thể để trống tên đăng nhập", html);
    }

    [Fact]
    public async Task Board_api_still_exposes_mark_paid_permission_for_checkout()
    {
        await EnsureEmployeeAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client);

        var board = await client.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.GetProperty("permissions").GetProperty("canMarkPaid").GetBoolean());
    }

    [Fact]
    public async Task Paid_order_still_exposes_payment_confirmed_by()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        await EnsureEmployeeAccountAsync();
        var employee = CreateClient();
        await LoginEmployeeAsync(employee);

        var orderId = await SubmitCashOrderAsync(employee, fixture);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var board = await employee.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(EmployeeDisplayName, paid.GetProperty("paymentConfirmedBy").GetString());
    }

    [Fact]
    public async Task Barista_can_still_complete_after_prep()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        var admin = CreateClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        var orderId = await SubmitCashOrderAsync(admin, fixture);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = CreateClient();
        await LoginSharedAsync(barista, "test-barista-secret-16");
        var board = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paidOrder = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        var itemId = paidOrder.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Checkout_employee_cannot_complete_or_prepare()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        await EnsureEmployeeAccountAsync();
        var employee = CreateClient();
        await LoginEmployeeAsync(employee);
        var orderId = await SubmitCashOrderAsync(employee, fixture);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var board = await employee.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.False(board.GetProperty("permissions").GetProperty("canComplete").GetBoolean());
        Assert.False(board.GetProperty("permissions").GetProperty("canPrepareItems").GetBoolean());

        var itemId = board.GetProperty("paid")[0].GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { prepared = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Orders_page_shows_operational_column_labels()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-staff-secret-16");

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync());
        Assert.Contains("Chờ thanh toán", html);
        Assert.Contains("Đang pha chế", html);
        Assert.Contains("Hoàn thành", html);
        Assert.Contains("Sàn phục vụ", html);
    }

    private async Task EnsureEmployeeAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(EmployeeUsername, EmployeePassword) is not null)
            return;

        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(EmployeeUsername, EmployeeDisplayName, EmployeePassword),
            "test-admin");
        Assert.Null(error);
        Assert.NotNull(account);
    }

    private static async Task<Guid> SubmitCashOrderAsync(HttpClient client, OrderTestSeedHelper.OrderSubmitFixture fixture)
    {
        var idemKey = Guid.NewGuid().ToString("N");
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
            paymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private HttpClient CreateClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private HttpClient CreateNoRedirectClient()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private static async Task LoginSharedAsync(HttpClient client, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Staff/Login");
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task LoginEmployeeAsync(HttpClient client)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Staff/Login");
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = EmployeeUsername,
            ["Password"] = EmployeePassword,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var get = await client.GetAsync(path);
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success);
        return tokenMatch.Groups["v"].Value;
    }
}
