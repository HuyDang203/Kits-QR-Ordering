using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class AdminStaffAccountsCredentialUxTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Staff_account_list_does_not_show_password_hash()
    {
        var username = UniqueUsername("hash-list");
        await CreateAccountViaAdminUiAsync(username, "Hash List QA", "TempPass123!");

        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, "/admin/staff-accounts");

        Assert.DoesNotContain("PasswordHash", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", html);
    }

    [Fact]
    public async Task Normal_get_does_not_show_existing_password()
    {
        const string password = "ExistingSecret99!";
        var username = UniqueUsername("no-show");
        await CreateAccountViaServiceAsync(username, "No Show QA", password);

        var admin = await LoginAdminAsync();
        var html = await GetHtmlAsync(admin, "/admin/staff-accounts");

        Assert.DoesNotContain(password, html);
        Assert.Contains("Vì lý do bảo mật", html);
    }

    [Fact]
    public async Task Create_account_success_shows_temporary_password_once()
    {
        const string password = "CreateOnce123!";
        var username = UniqueUsername("create-once");
        const string displayName = "Nguyen Van B";

        var admin = await LoginAdminAsync();
        var html = await PostCreateAccountAsync(admin, username, displayName, password);

        Assert.Contains(username, html);
        Assert.Contains("admin-staff-accounts-credential", html);
        Assert.Contains("credential__password", html);
        Assert.Contains(password, html);
        Assert.Contains(displayName, html);
        Assert.Contains("Mật khẩu tạm thời", html);
        Assert.Contains("Sao chép thông tin đăng nhập", html);
        Assert.Contains("/staff/login", html);

        var refresh = await GetHtmlAsync(admin, "/admin/staff-accounts");
        Assert.DoesNotContain(password, refresh);
    }

    [Fact]
    public async Task Create_account_stores_password_hash_not_plain_text()
    {
        const string password = "HashStore123!";
        var username = UniqueUsername("hash-store");

        await CreateAccountViaAdminUiAsync(username, "Hash Store QA", password);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.StaffAccounts.AsNoTracking().SingleAsync(a => a.Username == username);
        Assert.NotEqual(password, row.PasswordHash);

        var hasher = new PasswordHasher<Domain.Entities.StaffAccount>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(row, row.PasswordHash, password));
    }

    [Fact]
    public async Task Reset_password_success_shows_new_temporary_password_once()
    {
        const string oldPassword = "OldTemp12345!";
        const string newPassword = "NewTemp67890!";
        var username = UniqueUsername("reset-once");

        var account = await CreateAccountViaServiceAsync(username, "Reset Once QA", oldPassword);
        var admin = await LoginAdminAsync();
        var html = await PostResetPasswordAsync(admin, account.Id, newPassword);

        Assert.Contains("admin-staff-accounts-credential", html);
        Assert.Contains("credential__password", html);
        Assert.Contains(newPassword, html);
        Assert.DoesNotContain(oldPassword, html);

        var refresh = await GetHtmlAsync(admin, "/admin/staff-accounts");
        Assert.DoesNotContain(newPassword, refresh);
    }

    [Fact]
    public async Task Old_password_fails_after_reset_via_ui()
    {
        const string oldPassword = "OldUiPass123!";
        const string newPassword = "NewUiPass678!";
        var username = UniqueUsername("old-fail");

        var account = await CreateAccountViaServiceAsync(username, "Old Fail QA", oldPassword);
        var admin = await LoginAdminAsync();
        await PostResetPasswordAsync(admin, account.Id, newPassword);

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, username, oldPassword, expectSuccess: false);
    }

    [Fact]
    public async Task New_password_works_after_reset_via_ui()
    {
        const string oldPassword = "OldWorks12345!";
        const string newPassword = "NewWorks67890!";
        var username = UniqueUsername("new-works");

        var account = await CreateAccountViaServiceAsync(username, "New Works QA", oldPassword);
        var admin = await LoginAdminAsync();
        await PostResetPasswordAsync(admin, account.Id, newPassword);

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, username, newPassword);
        var res = await client.GetAsync("/staff/orders");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Inactive_account_still_cannot_login_after_reset()
    {
        const string newPassword = "Inactive6789!";
        var username = UniqueUsername("inactive-reset");

        var account = await CreateAccountViaServiceAsync(username, "Inactive Reset QA", "StartPass123!");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
            await svc.UpdateAsync(account.Id, new StaffAccountUpdateRequest(account.DisplayName, false));
        }

        var admin = await LoginAdminAsync();
        await PostResetPasswordAsync(admin, account.Id, newPassword);

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, username, newPassword, expectSuccess: false);
    }

    [Fact]
    public async Task Employee_permissions_unchanged_after_credential_ux()
    {
        var username = UniqueUsername("perm-check");
        await CreateAccountViaAdminUiAsync(username, "Perm Check QA", "PermCheck123!");

        var client = CreateClientWithPartition();
        await LoginEmployeeAsync(client, username, "PermCheck123!");
        var board = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/staff/orders");
        var perms = board.GetProperty("permissions");
        Assert.True(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.False(perms.GetProperty("canComplete").GetBoolean());
        Assert.False(perms.GetProperty("canPrepareItems").GetBoolean());
    }

    [Fact]
    public async Task Payment_attribution_unchanged_after_credential_ux()
    {
        var username = UniqueUsername("attrib-check");
        const string password = "AttribCheck123!";
        await CreateAccountViaAdminUiAsync(username, "Attrib Check QA", password);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        var guest = factory.CreateClient();
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = $"attrib-ux-{Guid.NewGuid():N}",
            paymentMethod = OrderPaymentMethods.CashOrCardAtCounter,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = System.Net.Http.Json.JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", payload.idempotencyKey);
        var orderId = (await (await guest.SendAsync(req)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetGuid();

        var employee = CreateClientWithPartition();
        await LoginEmployeeAsync(employee, username, password);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal("Attrib Check QA", order.PaymentConfirmedBy);
        Assert.NotNull(order.PaymentConfirmedByAccountId);
    }

    private static string UniqueUsername(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..24].ToLowerInvariant();

    private async Task<HttpClient> LoginAdminAsync()
    {
        var client = CreateAdminClient();
        await LoginSharedAsync(client, "test-staff-secret-16");
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private async Task<Domain.Entities.StaffAccount> CreateAccountViaServiceAsync(
        string username,
        string displayName,
        string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IStaffAccountService>();
        var (account, error) = await svc.CreateAsync(
            new StaffAccountCreateRequest(username, displayName, password),
            "test-admin");
        Assert.Null(error);
        return account!;
    }

    private async Task CreateAccountViaAdminUiAsync(string username, string displayName, string password)
    {
        var admin = await LoginAdminAsync();
        await PostCreateAccountAsync(admin, username, displayName, password);
    }

    private static async Task<string> PostCreateAccountAsync(
        HttpClient admin,
        string username,
        string displayName,
        string password)
    {
        var token = await GetPageAntiforgeryTokenAsync(admin, "/admin/staff-accounts");
        var form = new Dictionary<string, string?>
        {
            ["CreateUsername"] = username,
            ["CreateDisplayName"] = displayName,
            ["CreatePassword"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await admin.PostAsync("/admin/staff-accounts?handler=Create", new FormUrlEncodedContent(form!));
        post.EnsureSuccessStatusCode();
        return await post.Content.ReadAsStringAsync();
    }

    private static async Task<string> PostResetPasswordAsync(
        HttpClient admin,
        Guid accountId,
        string newPassword)
    {
        var token = await GetPageAntiforgeryTokenAsync(admin, $"/admin/staff-accounts?edit={accountId:D}");
        var form = new Dictionary<string, string?>
        {
            ["ResetAccountId"] = accountId.ToString("D"),
            ["ResetPassword"] = newPassword,
            ["__RequestVerificationToken"] = token
        };
        var post = await admin.PostAsync("/admin/staff-accounts?handler=ResetPassword", new FormUrlEncodedContent(form!));
        post.EnsureSuccessStatusCode();
        return await post.Content.ReadAsStringAsync();
    }

    private HttpClient CreateClientWithPartition()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private HttpClient CreateNoRedirectClient()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
    }

    private static async Task LoginSharedAsync(HttpClient client, string password)
    {
        var token = await GetPageAntiforgeryTokenAsync(client, "/Staff/Login");
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
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
        var token = await GetPageAntiforgeryTokenAsync(client, "/Staff/Login");
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

    private static async Task<string> GetPageAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var get = await client.GetAsync(path);
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success, "Antiforgery token not found.");
        return tokenMatch.Groups["v"].Value;
    }
}
