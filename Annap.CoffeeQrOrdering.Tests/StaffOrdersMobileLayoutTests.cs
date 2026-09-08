using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffOrdersMobileLayoutTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string BaristaUsername = "mobile-barista";
    private const string BaristaPassword = "Test12345!";
    private const string CheckoutUsername = "mobile-checkout";
    private const string CheckoutPassword = "Test12345!";

    [Fact]
    public async Task Orders_html_includes_mobile_tabs()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-staff-secret-16");
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("staff-mobile-tabs", html);
        Assert.Contains("data-staff-mobile-tab=\"submitted\"", html);
        Assert.Contains("data-staff-mobile-tab=\"paid\"", html);
        Assert.Contains("data-staff-mobile-tab=\"completed\"", html);
    }

    [Fact]
    public async Task EmployeeCheckout_default_mobile_tab_is_submitted()
    {
        await EnsureCheckoutAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, CheckoutUsername, CheckoutPassword);
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("data-staff-default-tab=\"submitted\"", html);
        Assert.Contains("staff-col--submitted staff-col--active", html);
    }

    [Fact]
    public async Task EmployeeBarista_default_mobile_tab_is_paid()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("data-staff-default-tab=\"paid\"", html);
        Assert.Contains("staff-col--paid staff-col--active", html);
    }

    [Fact]
    public async Task Shared_barista_default_mobile_tab_is_paid()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-barista-secret-16");
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("data-staff-default-tab=\"paid\"", html);
    }

    [Fact]
    public async Task Checkout_sees_ket_ca_in_mobile_header()
    {
        await EnsureCheckoutAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, CheckoutUsername, CheckoutPassword);
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("staff-mobile-header__btn--shift", html);
        Assert.Contains("Kết ca", html);
    }

    [Fact]
    public async Task Barista_does_not_see_ket_ca_in_mobile_header()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("staff-mobile-header__btn--shift", html);
        Assert.DoesNotContain("/staff/shift-close", html);
    }

    [Fact]
    public async Task Checkout_board_denies_prepare_and_complete()
    {
        await EnsureCheckoutAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, CheckoutUsername, CheckoutPassword);
        var board = await client.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var perms = board.GetProperty("permissions");
        Assert.False(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.False(perms.GetProperty("canComplete").GetBoolean());
    }

    [Fact]
    public async Task Barista_board_denies_mark_paid()
    {
        await EnsureBaristaAccountAsync();
        var client = CreateClient();
        await LoginEmployeeAsync(client, BaristaUsername, BaristaPassword);
        var board = await client.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.False(board.GetProperty("permissions").GetProperty("canMarkPaid").GetBoolean());
    }

    [Fact]
    public async Task Staff_board_js_uses_compact_wait_time_labels()
    {
        var res = await factory.CreateClient().GetAsync("/js/staff-orders-board.js");
        res.EnsureSuccessStatusCode();
        var js = await res.Content.ReadAsStringAsync();
        Assert.Contains("Cũ ·", js);
        Assert.Contains("giờ", js);
        Assert.Contains("phút", js);
    }

    [Fact]
    public async Task Staff_board_css_uses_mobile_tab_visibility()
    {
        var res = await factory.CreateClient().GetAsync("/css/staff-board.css");
        res.EnsureSuccessStatusCode();
        var css = await res.Content.ReadAsStringAsync();
        Assert.Contains(".staff-col.staff-col--active", css);
        Assert.Contains("grid-template-columns: repeat(3", css);
    }

    [Fact]
    public async Task Mobile_header_includes_switch_employee_action()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "test-staff-secret-16");
        var html = await (await client.GetAsync("/staff/orders")).Content.ReadAsStringAsync();
        Assert.Contains("staff-mobile-header", html);
        Assert.Contains("Đổi nhân viên", html);
    }

    private async Task EnsureBaristaAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(BaristaUsername, BaristaPassword) is not null)
            return;
        var (_, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(BaristaUsername, "Trần Pha Chế", BaristaPassword, StaffAccountRoles.EmployeeBarista),
            "test-admin");
        Assert.Null(error);
    }

    private async Task EnsureCheckoutAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        if (await svc.AuthenticateAsync(CheckoutUsername, CheckoutPassword) is not null)
            return;
        var (_, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(CheckoutUsername, "Nguyễn Thu Ngân", CheckoutPassword, StaffAccountRoles.EmployeeCheckout),
            "test-admin");
        Assert.Null(error);
    }

    private HttpClient CreateClient()
    {
        var client = factory.CreateClient();
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
}
