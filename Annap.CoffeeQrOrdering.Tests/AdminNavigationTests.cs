using System.Net;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class AdminNavigationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string EmployeeUsername = "nav-qa-employee";
    private const string EmployeePassword = "Test12345!";

    [Fact]
    public async Task Admin_home_contains_quick_links_to_reports_payments_and_staff_accounts()
    {
        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, "/admin");

        Assert.Contains("/admin/reports", html);
        Assert.Contains("/admin/payments", html);
        Assert.Contains("/admin/staff-accounts", html);
        Assert.Contains("Truy cập nhanh", html);
        Assert.Contains("Báo cáo bán hàng", html);
        Assert.Contains("Đối soát chuyển khoản", html);
        Assert.Contains("Tài khoản nhân viên", html);
    }

    [Fact]
    public async Task Admin_navigation_contains_reports_payments_and_staff_accounts()
    {
        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, "/admin/reports");

        Assert.Contains("/admin/reports", html);
        Assert.Contains("/admin/payments", html);
        Assert.Contains("/admin/staff-accounts", html);
        Assert.Contains("Báo cáo bán hàng", html);
        Assert.Contains("Đối soát chuyển khoản", html);
        Assert.Contains("Tài khoản nhân viên", html);
    }

    [Theory]
    [InlineData("/admin/reports", "Báo cáo")]
    [InlineData("/admin/payments", "Đối soát chuyển khoản")]
    [InlineData("/admin/staff-accounts", "Tài khoản nhân viên")]
    public async Task Admin_page_highlights_active_nav_section(string path, string pageMarker)
    {
        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, path);

        Assert.Contains(pageMarker, html);
        Assert.Contains("admin-nav-link is-active", html);
        Assert.Contains($"href=\"{path}\"", html);
    }

    [Theory]
    [InlineData("test-checkout-secret-16")]
    [InlineData("test-barista-secret-16")]
    public async Task Shared_staff_roles_cannot_access_admin_reports_payments_or_staff_accounts(string password)
    {
        var client = CreateNoRedirectClient();
        await LoginSharedAsync(client, password);

        foreach (var path in new[] { "/admin/reports", "/admin/payments", "/admin/staff-accounts" })
        {
            var res = await client.GetAsync(path);
            AssertDenied(res);
        }
    }

    [Fact]
    public async Task Employee_checkout_cannot_access_admin_reports_payments_or_staff_accounts()
    {
        await EnsureEmployeeAccountAsync();
        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client);

        foreach (var path in new[] { "/admin/reports", "/admin/payments", "/admin/staff-accounts" })
        {
            var res = await client.GetAsync(path);
            AssertDenied(res);
        }
    }

    [Fact]
    public async Task Admin_can_access_all_major_linked_admin_routes()
    {
        var admin = await LoginAdminAsync();
        var routes = new[]
        {
            "/admin",
            "/admin/reports",
            "/admin/payments",
            "/admin/staff-accounts",
            "/admin/tables",
            "/admin/demo/qr",
            "/admin/operations",
            "/admin/menu",
            "/admin/inventory",
            "/admin/intelligence"
        };

        foreach (var route in routes)
        {
            var res = await admin.GetAsync(route);
            Assert.True(
                res.IsSuccessStatusCode,
                $"Expected 200 for {route}, got {res.StatusCode}");
        }
    }

    [Fact]
    public async Task Admin_navigation_does_not_link_to_missing_routes()
    {
        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, "/admin");

        var hrefs = Regex.Matches(html, @"href=""(?<h>/admin[^""#?]+)""", RegexOptions.IgnoreCase)
            .Select(m => m.Groups["h"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(hrefs);

        foreach (var href in hrefs)
        {
            var res = await admin.GetAsync(href);
            Assert.True(
                res.IsSuccessStatusCode,
                $"Nav href {href} returned {res.StatusCode}");
        }
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        var client = CreateNoRedirectClient();
        await LoginSharedAsync(client, "test-staff-secret-16");
        return client;
    }

    private async Task EnsureEmployeeAccountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var existing = await svc.AuthenticateAsync(EmployeeUsername, EmployeePassword);
        if (existing is not null)
            return;

        var (_, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(EmployeeUsername, "Nav QA Employee", EmployeePassword),
            "test-admin");
        Assert.Null(error);
    }

    private HttpClient CreateNoRedirectClient()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
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
