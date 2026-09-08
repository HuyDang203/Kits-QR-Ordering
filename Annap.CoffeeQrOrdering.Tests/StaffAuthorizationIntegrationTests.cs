using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffAuthorizationIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Anonymous_cannot_mark_paid()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-anon-mark-1");
        var res = await factory.CreateClient().PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_prepare_item()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-anon-prep-1");
        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await factory.CreateClient().PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_complete_order()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-anon-complete-1");
        var res = await factory.CreateClient().PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_view_staff_bill()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-anon-bill-1");
        var res = await factory.CreateClient().GetAsync($"/api/staff/orders/{orderId}/bill");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Checkout_can_mark_paid()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-checkout-mark-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var res = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Barista_can_prepare_paid_item()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-barista-prep-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Barista_can_complete_prepared_paid_order()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-barista-complete-1");

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var itemId = await GetFirstItemIdAsync(orderId);
        Assert.Equal(HttpStatusCode.OK, (await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true })).StatusCode);

        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_mark_paid_and_complete()
    {
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(factory.CreateClient(), fixture, "auth-admin-flow-1");

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var itemId = await GetFirstItemIdAsync(orderId);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);
    }

    [Theory]
    [InlineData("test-checkout-secret-16")]
    [InlineData("test-barista-secret-16")]
    public async Task Non_admin_cannot_access_admin_reports(string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginStaffAsync(client, "test-host", password);
        var res = await client.GetAsync("/admin/reports");
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    [Theory]
    [InlineData("test-checkout-secret-16", "/admin/menu")]
    [InlineData("test-barista-secret-16", "/admin")]
    public async Task Non_admin_cannot_access_admin_pages(string password, string path)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginStaffAsync(client, "test-host", password);
        var res = await client.GetAsync(path);
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode} for {path}");
    }

    [Fact]
    public async Task Anonymous_staff_orders_page_redirects_to_login()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync("/Staff/Orders");
        Assert.True(
            res.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized,
            $"Unexpected status {res.StatusCode}");
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private static async Task<Guid> SubmitOrderAsync(
        HttpClient client,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey)
    {
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
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

    private static async Task LoginStaffAsync(HttpClient client, string user, string password)
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
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }
}
