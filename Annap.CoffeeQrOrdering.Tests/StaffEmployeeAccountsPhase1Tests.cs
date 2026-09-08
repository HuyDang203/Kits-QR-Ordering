using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffEmployeeAccountsPhase1Tests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string EmployeeUsername = "thu-ngan-test";
    private const string EmployeeDisplayName = "Nguyễn Văn A";
    private const string EmployeePassword = "Test12345!";
    private static string EmployeeLoginName(string suffix) => $"{EmployeeUsername}-{suffix}";

    [Fact]
    public async Task Admin_can_access_staff_accounts_page()
    {
        var admin = CreateNoRedirectClient();
        await LoginSharedAsync(admin, "test-host", "test-staff-secret-16");

        var res = await admin.GetAsync("/admin/staff-accounts");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Tài khoản nhân viên", html);
    }

    [Fact]
    public async Task Checkout_shared_cannot_access_staff_accounts()
    {
        var checkout = CreateNoRedirectClient();
        await LoginSharedAsync(checkout, "test-host", "test-checkout-secret-16");
        var res = await checkout.GetAsync("/admin/staff-accounts");
        AssertDenied(res);
    }

    [Fact]
    public async Task Barista_cannot_access_staff_accounts()
    {
        var barista = CreateNoRedirectClient();
        await LoginSharedAsync(barista, "test-host", "test-barista-secret-16");
        var res = await barista.GetAsync("/admin/staff-accounts");
        AssertDenied(res);
    }

    [Fact]
    public async Task Anonymous_cannot_access_staff_accounts()
    {
        var anon = CreateNoRedirectClient();
        var res = await anon.GetAsync("/admin/staff-accounts");
        Assert.True(
            res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    [Fact]
    public async Task Admin_can_create_staff_account_via_service()
    {
        var account = await CreateEmployeeAccountAsync("create-via-svc");
        Assert.Equal(EmployeeDisplayName, account.DisplayName);
        Assert.Equal(StaffAccountRoles.EmployeeCheckout, account.Role);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task Duplicate_username_rejected()
    {
        var username = $"dup-user-{Guid.NewGuid():N}"[..24];
        await CreateEmployeeAccountWithUsernameAsync(username);
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var (_, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(username, "Other Name", EmployeePassword),
            "admin",
            CancellationToken.None);
        Assert.NotNull(error);
        Assert.Contains("tồn tại", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Password_is_hashed_not_plain()
    {
        var account = await CreateEmployeeAccountAsync("hash-check");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.StaffAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.NotEqual(EmployeePassword, row.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(row.PasswordHash));

        var hasher = new PasswordHasher<StaffAccount>();
        var verify = hasher.VerifyHashedPassword(row, row.PasswordHash, EmployeePassword);
        Assert.Equal(PasswordVerificationResult.Success, verify);
    }

    [Fact]
    public async Task Inactive_account_cannot_login()
    {
        var account = await CreateEmployeeAccountAsync("inactive-user");
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        await svc.UpdateAsync(account.Id, new StaffAccountUpdateRequest(account.DisplayName, false));

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("inactive-user"), EmployeePassword, expectSuccess: false);
    }

    [Fact]
    public async Task Password_reset_works()
    {
        var account = await CreateEmployeeAccountAsync("reset-pw");
        const string newPassword = "NewPass9876!";

        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var (ok, error) = await svc.ResetPasswordAsync(account.Id, newPassword);
        Assert.True(ok, error);

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("reset-pw"), newPassword);
    }

    [Fact]
    public async Task Employee_account_can_login_with_username_password()
    {
        await CreateEmployeeAccountAsync("login-ok");
        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("login-ok"), EmployeePassword);
    }

    [Fact]
    public async Task Invalid_password_fails_login()
    {
        await CreateEmployeeAccountAsync("bad-pw");
        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("bad-pw"), "WrongPass99!", expectSuccess: false);
    }

    [Theory]
    [InlineData("test-staff-secret-16")]
    [InlineData("test-checkout-secret-16")]
    [InlineData("test-barista-secret-16")]
    public async Task Existing_shared_password_logins_still_work(string password)
    {
        var client = CreateClientWithPartition();
        await LoginSharedAsync(client, "test-host", password);
        var res = await client.GetAsync("/Staff/Orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Employee_can_access_staff_orders_page()
    {
        await CreateEmployeeAccountAsync("page-access");
        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("page-access"), EmployeePassword);
        var res = await client.GetAsync("/Staff/Orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Employee_can_get_staff_board_api()
    {
        await CreateEmployeeAccountAsync("board-api");
        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, EmployeeLoginName("board-api"), EmployeePassword);
        var board = await client.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.TryGetProperty("submitted", out _));
        Assert.True(board.TryGetProperty("permissions", out var perms));
        Assert.True(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.False(perms.GetProperty("canComplete").GetBoolean());
        Assert.False(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.False(perms.GetProperty("canManageBills").GetBoolean());
    }

    [Fact]
    public async Task Employee_can_mark_paid()
    {
        await CreateEmployeeAccountAsync("mark-paid");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-mark-paid-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("mark-paid"), EmployeePassword);
        var res = await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_prepare_item()
    {
        await CreateEmployeeAccountAsync("no-prep");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-no-prep-1");

        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        await LoginSharedAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("no-prep"), EmployeePassword);
        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await employee.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_complete_order()
    {
        await CreateEmployeeAccountAsync("no-complete");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-no-complete-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("no-complete"), EmployeePassword);
        await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        var res = await employee.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_access_admin_reports()
    {
        await CreateEmployeeAccountAsync("no-reports");
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, EmployeeLoginName("no-reports"), EmployeePassword);
        var res = await client.GetAsync("/admin/reports");
        AssertDenied(res);
    }

    [Fact]
    public async Task Employee_cannot_access_admin_payments()
    {
        await CreateEmployeeAccountAsync("no-payments");
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, EmployeeLoginName("no-payments"), EmployeePassword);
        var res = await client.GetAsync("/admin/payments");
        AssertDenied(res);
    }

    [Fact]
    public async Task Employee_cannot_access_admin_staff_accounts()
    {
        await CreateEmployeeAccountAsync("no-admin-accounts");
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, EmployeeLoginName("no-admin-accounts"), EmployeePassword);
        var res = await client.GetAsync("/admin/staff-accounts");
        AssertDenied(res);
    }

    [Fact]
    public async Task Employee_cannot_use_legacy_patch_status()
    {
        await CreateEmployeeAccountAsync("no-legacy");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-legacy-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("no-legacy"), EmployeePassword);
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/staff/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { status = "served" })
        };
        var res = await employee.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Mark_paid_by_employee_stores_display_name_and_account_id()
    {
        await CreateEmployeeAccountAsync("attrib-emp");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-attrib-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("attrib-emp"), EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(EmployeeDisplayName, order.PaymentConfirmedBy);
        Assert.NotNull(order.PaymentConfirmedByAccountId);
    }

    [Fact]
    public async Task Staff_board_paid_card_includes_confirmer_name()
    {
        await CreateEmployeeAccountAsync("board-attrib");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-board-attrib-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("board-attrib"), EmployeePassword);
        await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        var board = await employee.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(EmployeeDisplayName, paid.GetProperty("paymentConfirmedBy").GetString());
    }

    [Fact]
    public async Task Bill_detail_includes_confirmer_name()
    {
        await CreateEmployeeAccountAsync("bill-attrib");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-bill-attrib-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("bill-attrib"), EmployeePassword);
        await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        var bill = await employee.GetFromJsonAsync<JsonElement>($"/api/staff/orders/{orderId}/bill");
        Assert.Equal(EmployeeDisplayName, bill.GetProperty("paymentConfirmedBy").GetString());
    }

    [Fact]
    public async Task Shared_checkout_mark_paid_stores_kiem_don_fallback()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "shared-attrib-1");

        var checkout = factory.CreateClient();
        await LoginSharedAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Nhân viên kiểm đơn", order.PaymentConfirmedBy);
        Assert.Null(order.PaymentConfirmedByAccountId);
    }

    [Fact]
    public async Task Shared_admin_mark_paid_stores_quan_ly_fallback()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "admin-attrib-1");

        var admin = factory.CreateClient();
        await LoginSharedAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Quản lý", order.PaymentConfirmedBy);
        Assert.Null(order.PaymentConfirmedByAccountId);
    }

    [Fact]
    public async Task BankTransfer_manual_confirmation_stores_employee_display_name()
    {
        await CreateEmployeeAccountAsync("bank-attrib");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-bank-attrib-1", OrderPaymentMethods.BankTransfer);

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("bank-attrib"), EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.BankTransfer, order.PaymentMethod);
        Assert.Equal(EmployeeDisplayName, order.PaymentConfirmedBy);
        Assert.NotNull(order.PaymentConfirmedByAccountId);

        var board = await employee.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(EmployeeDisplayName, paid.GetProperty("paymentConfirmedBy").GetString());
    }

    [Fact]
    public async Task Mark_paid_retry_does_not_overwrite_confirmer()
    {
        await CreateEmployeeAccountAsync("retry-attrib");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-retry-attrib-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("retry-attrib"), EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var admin = factory.CreateClient();
        await LoginSharedAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(EmployeeDisplayName, order.PaymentConfirmedBy);
    }

    [Fact]
    public async Task Staff_board_includes_payment_confirmed_by_account_id()
    {
        var account = await CreateEmployeeAccountAsync("board-acct-id");
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(fixture, "emp-board-acct-id-1");

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, EmployeeLoginName("board-acct-id"), EmployeePassword);
        await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        var board = await employee.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(account.Id, paid.GetProperty("paymentConfirmedByAccountId").GetGuid());
    }

    private HttpClient CreateClientWithPartition()
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

    private async Task<StaffAccount> CreateEmployeeAccountAsync(string usernameSuffix) =>
        await CreateEmployeeAccountWithUsernameAsync($"{EmployeeUsername}-{usernameSuffix}");

    private async Task<StaffAccount> CreateEmployeeAccountWithUsernameAsync(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(username, EmployeeDisplayName, EmployeePassword),
            "test-admin");
        Assert.Null(error);
        return account!;
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
        string? paymentMethod = null)
    {
        var client = factory.CreateClient();
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
            paymentMethod = paymentMethod ?? OrderPaymentMethods.CashOrCardAtCounter,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> GetFirstItemIdAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.Id)
            .FirstAsync();
    }

    private static void AssertDenied(HttpResponseMessage res)
    {
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    private static async Task LoginSharedAsync(HttpClient client, string user, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task LoginEmployeeAsync(
        HttpClient client,
        string username,
        string password,
        bool expectSuccess = true)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = username,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        if (expectSuccess)
        {
            Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
            return;
        }

        var html = await post.Content.ReadAsStringAsync();
        StaffLoginTestAssertions.AssertLoginFailureHtml(html);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var get = await client.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        return tokenMatch.Groups["v"].Value;
    }
}

public sealed class BankTransferFrozenPhase1Tests(BankTransferDisabledPostgresWebApplicationFactory factory)
    : IClassFixture<BankTransferDisabledPostgresWebApplicationFactory>
{
    [Fact]
    public async Task BankTransfer_availability_returns_unavailable_when_disabled()
    {
        var client = factory.CreateClient();
        var avail = await client.GetFromJsonAsync<JsonElement>("/api/guest/bank-transfer");
        Assert.False(avail.GetProperty("enabled").GetBoolean());
        Assert.Equal("unavailable", avail.GetProperty("status").GetString());
        Assert.Contains("Chuyển khoản hiện chưa khả dụng", avail.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Cash_card_checkout_still_works_when_bank_transfer_disabled()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        var client = factory.CreateClient();
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = "bt-frozen-cash-1",
            paymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", "bt-frozen-cash-1");
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.CashOrCardAtCounter, order.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, order.Status);
    }
}
