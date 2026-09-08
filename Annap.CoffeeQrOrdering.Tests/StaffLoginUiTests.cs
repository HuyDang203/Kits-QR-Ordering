using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffLoginUiTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private const string EmployeePassword = "Test12345!";
    private static string EmployeeUsername(string suffix) => $"login-ui-{suffix}";

    [Fact]
    public async Task Login_page_contains_username_field()
    {
        var html = await GetLoginHtmlAsync();
        Assert.Contains("id=\"UserName\"", html);
        Assert.Contains("Tên đăng nhập", html);
    }

    [Fact]
    public async Task Login_page_contains_password_field()
    {
        var html = await GetLoginHtmlAsync();
        Assert.Contains("id=\"Password\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("Mật khẩu", html);
    }

    [Fact]
    public async Task Login_page_mentions_employee_account_login()
    {
        var html = await GetLoginHtmlAsync();
        Assert.Contains("Đăng nhập quầy", html);
        Assert.Contains("Nhân viên dùng tên đăng nhập", html);
        Assert.Contains("thu-ngan-1", html);
        Assert.Contains("có thể để trống tên đăng nhập", html);
    }

    [Fact]
    public async Task Employee_can_login_with_username_and_password()
    {
        var username = EmployeeUsername("ok");
        await EnsureEmployeeAsync(username);

        var client = CreateClient();
        await LoginEmployeeAsync(client, username, EmployeePassword);
        var res = await client.GetAsync("/staff/orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Empty_username_shared_password_fallback_still_works()
    {
        var client = CreateClient();
        await LoginSharedAsync(client, "", "test-staff-secret-16");
        var res = await client.GetAsync("/staff/orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Inactive_employee_cannot_login()
    {
        var username = EmployeeUsername("inactive");
        var account = await EnsureEmployeeAsync(username);

        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        await svc.UpdateAsync(account.Id, new StaffAccountUpdateRequest(account.DisplayName, false));

        var client = CreateClient();
        await LoginEmployeeAsync(client, username, EmployeePassword, expectSuccess: false);
    }

    [Fact]
    public async Task Employee_login_gets_checkout_access()
    {
        var username = EmployeeUsername("checkout");
        await EnsureEmployeeAsync(username);

        var client = CreateClient();
        await LoginEmployeeAsync(client, username, EmployeePassword);
        var board = await client.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.GetProperty("permissions").GetProperty("canMarkPaid").GetBoolean());
    }

    [Fact]
    public async Task Employee_login_cannot_access_admin()
    {
        var username = EmployeeUsername("no-admin");
        await EnsureEmployeeAsync(username);

        var client = CreateNoRedirectClient();
        await LoginEmployeeAsync(client, username, EmployeePassword);
        var res = await client.GetAsync("/admin");
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    [Fact]
    public async Task Failed_login_shows_vietnamese_error()
    {
        var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "not-a-real-user",
            ["Password"] = "WrongPass99!",
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/staff/login", new FormUrlEncodedContent(form!));
        var html = await post.Content.ReadAsStringAsync();
        StaffLoginTestAssertions.AssertLoginFailureHtml(html);
    }

    private async Task<string> GetLoginHtmlAsync()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/staff/login");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
    }

    private async Task<Domain.Entities.StaffAccount> EnsureEmployeeAsync(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var existing = await svc.AuthenticateAsync(username, EmployeePassword);
        if (existing is not null)
            return existing;

        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(username, "Login UI QA", EmployeePassword),
            "test-admin");
        Assert.Null(error);
        return account!;
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

    private static async Task LoginSharedAsync(HttpClient client, string userName, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = userName,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/staff/login", new FormUrlEncodedContent(form!));
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
        var post = await client.PostAsync("/staff/login", new FormUrlEncodedContent(form!));
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
        var get = await client.GetAsync("/staff/login");
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
