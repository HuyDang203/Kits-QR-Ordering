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

public sealed class StaffBaristaEmployeeAccountsTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string BaristaUsername = "pha-che-test";
    private const string BaristaDisplayName = "Trần Pha Chế";
    private const string BaristaPassword = "Test12345!";
    private const string CheckoutUsername = "thu-ngan-barista-qa";
    private const string CheckoutDisplayName = "Nguyễn Thu Ngân";
    private const string CheckoutPassword = "Test12345!";

    [Fact]
    public async Task Admin_can_create_EmployeeBarista_account()
    {
        var account = await CreateAccountAsync("barista-create", BaristaDisplayName, StaffAccountRoles.EmployeeBarista);
        Assert.Equal(StaffAccountRoles.EmployeeBarista, account.Role);
    }

    [Fact]
    public async Task Invalid_role_rejected_on_create()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest("bad-role-user", "Test", CheckoutPassword, "InvalidRole"),
            "admin");
        Assert.Null(account);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Admin_staff_account_list_shows_barista_role_label()
    {
        await CreateAccountAsync("barista-list", BaristaDisplayName, StaffAccountRoles.EmployeeBarista);
        var admin = CreateNoRedirectClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        var html = WebUtility.HtmlDecode(await (await admin.GetAsync("/admin/staff-accounts")).Content.ReadAsStringAsync());
        Assert.Contains("Pha chế", html);
    }

    [Fact]
    public async Task EmployeeBarista_can_login()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        var res = await client.GetAsync("/staff/orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task EmployeeBarista_cannot_access_admin()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        AssertDenied(await client.GetAsync("/admin"));
    }

    [Fact]
    public async Task EmployeeBarista_cannot_access_shift_close()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        AssertDenied(await client.GetAsync("/staff/shift-close"));
    }

    [Fact]
    public async Task EmployeeBarista_orders_page_shows_identity_and_hides_admin_and_ket_ca()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync());
        Assert.Contains("Đang đăng nhập: Trần Pha Chế", html);
        Assert.Contains("@pha-che-test · Pha chế", html);
        Assert.DoesNotContain("/staff/shift-close", html);
        Assert.DoesNotContain("/admin", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmployeeBarista_cannot_mark_paid()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        await EnsureBaristaAccountAsync();
        var barista = CreateClient();
        await LoginEmployeeAsync(barista, BaristaUsername, BaristaPassword);
        var res = await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task EmployeeBarista_can_prepare_and_complete_with_attribution()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        await EnsureCheckoutAccountAsync();
        var checkout = CreateClient();
        await LoginEmployeeAsync(checkout, CheckoutUsername, CheckoutPassword);
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await EnsureBaristaAccountAsync();
        var barista = CreateClient();
        await LoginEmployeeAsync(barista, BaristaUsername, BaristaPassword);
        var board = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paidOrder = FindPaidOrder(board, orderId);
        var itemId = paidOrder.GetProperty("items")[0].GetProperty("id").GetGuid();

        var prep = await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 });
        prep.EnsureSuccessStatusCode();

        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        Assert.Equal(BaristaDisplayName, item.PreparedBy);
        Assert.NotNull(item.PreparedByAccountId);

        var boardAfterPrep = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var apiItem = FindPaidOrder(boardAfterPrep, orderId).GetProperty("items")[0];
        Assert.Equal(BaristaDisplayName, apiItem.GetProperty("preparedBy").GetString());

        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(BaristaDisplayName, order.CompletedBy);
        Assert.NotNull(order.CompletedByAccountId);

        var doneBoard = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var completed = doneBoard.GetProperty("completed").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(BaristaDisplayName, completed.GetProperty("completedBy").GetString());
    }

    [Fact]
    public async Task EmployeeBarista_cannot_complete_before_all_items_prepared()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        var admin = CreateClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        await EnsureBaristaAccountAsync();
        var barista = CreateClient();
        await LoginEmployeeAsync(barista, BaristaUsername, BaristaPassword);
        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("items_not_prepared", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EmployeeCheckout_cannot_prepare_or_complete()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        await EnsureCheckoutAccountAsync();
        var checkout = CreateClient();
        await LoginEmployeeAsync(checkout, CheckoutUsername, CheckoutPassword);
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var itemId = FindPaidOrder(board, orderId).GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await checkout.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await checkout.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Shared_barista_prepare_uses_pha_che_fallback()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        var admin = CreateClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = CreateClient();
        await LoginSharedAsync(barista, "test-barista-secret-16");
        var board = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var itemId = FindPaidOrder(board, orderId).GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 })).StatusCode);

        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        Assert.Equal("Pha chế", item.PreparedBy);
        Assert.Null(item.PreparedByAccountId);

        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Pha chế", order.CompletedBy);
    }

    [Fact]
    public async Task Idempotent_prepare_does_not_overwrite_attribution()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        var admin = CreateClient();
        await LoginSharedAsync(admin, "test-staff-secret-16");
        await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        await EnsureBaristaAccountAsync();
        var barista = CreateClient();
        await LoginEmployeeAsync(barista, BaristaUsername, BaristaPassword);
        var board = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var itemId = FindPaidOrder(board, orderId).GetProperty("items")[0].GetProperty("id").GetGuid();

        await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/items/{itemId}/prepared", new { preparedQuantity = 1 });
        var first = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId);

        await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/items/{itemId}/prepared", new { preparedQuantity = 1 });
        var second = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId);

        Assert.Equal(first.PreparedBy, second.PreparedBy);
        Assert.Equal(first.PreparedByAccountId, second.PreparedByAccountId);
    }

    [Fact]
    public async Task EmployeeCheckout_payment_attribution_still_works()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var orderId = await SubmitCashOrderAsync(fixture);

        await EnsureCheckoutAccountAsync();
        var checkout = CreateClient();
        await LoginEmployeeAsync(checkout, CheckoutUsername, CheckoutPassword);
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(CheckoutDisplayName, order.PaymentConfirmedBy);
        Assert.NotNull(order.PaymentConfirmedByAccountId);
    }

    private async Task EnsureBaristaAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(BaristaUsername, BaristaPassword) is not null)
            return;
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(BaristaUsername, BaristaDisplayName, BaristaPassword, StaffAccountRoles.EmployeeBarista),
            "test-admin");
        Assert.Null(error);
        Assert.NotNull(account);
    }

    private async Task EnsureCheckoutAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(CheckoutUsername, CheckoutPassword) is not null)
            return;
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(CheckoutUsername, CheckoutDisplayName, CheckoutPassword, StaffAccountRoles.EmployeeCheckout),
            "test-admin");
        Assert.Null(error);
        Assert.NotNull(account);
    }

    private async Task<StaffAccount> CreateAccountAsync(string username, string displayName, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var unique = $"{username}-{Guid.NewGuid():N}"[..24];
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(unique, displayName, CheckoutPassword, role),
            "test-admin");
        Assert.Null(error);
        return account!;
    }

    private async Task<Guid> SubmitCashOrderAsync(OrderTestSeedHelper.OrderSubmitFixture fixture)
    {
        var client = factory.CreateClient();
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

    private static async Task LoginEmployeeAsync(HttpClient client, string username, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Staff/Login");
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = username,
            ["Password"] = password,
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

    private static void AssertDenied(HttpResponseMessage res)
    {
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    private static JsonElement FindPaidOrder(JsonElement board, Guid orderId) =>
        board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
}
