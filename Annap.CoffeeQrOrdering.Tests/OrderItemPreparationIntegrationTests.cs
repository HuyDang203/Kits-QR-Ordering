using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class OrderItemPreparationIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private HttpClient CreateClient(bool allowRedirect = true) =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowRedirect });

    private HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        var client = factory.CreateClient(options);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", Guid.NewGuid().ToString("N"));
        return client;
    }

    [Fact]
    public async Task Admin_can_access_admin_index()
    {
        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        var res = await admin.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Barista_cannot_access_admin_index()
    {
        var barista = CreateClient(allowRedirect: false);
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var res = await barista.GetAsync("/admin");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    [Fact]
    public async Task Checkout_cannot_tick_prepared_item()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-checkout-tick-1");

        var checkout = CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await checkout.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Barista_cannot_tick_item_on_submitted_order()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-barista-unpaid-1");

        var barista = CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Barista_cannot_complete_until_all_items_prepared()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-complete-block-1", quantity: 2);

        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("items_not_prepared", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Barista_can_complete_after_all_items_prepared()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-complete-ok-1", quantity: 2);

        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        await PrepareAllItemsAsync(barista, orderId);

        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PreparedQuantity_cannot_exceed_quantity()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-clamp-1");

        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await barista.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 99 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        Assert.Equal(1, item.PreparedQuantity);
    }

    [Fact]
    public async Task Completed_order_locks_preparation_editing()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-lock-1");

        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
        await PrepareAllItemsAsync(admin, orderId);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);

        var itemId = await GetFirstItemIdAsync(orderId);
        var res = await admin.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { isPrepared = false });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Board_includes_preparation_state_and_permissions()
    {
        var guest = CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "prep-board-1", quantity: 2);

        var admin = CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var board = await admin.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var perms = board.GetProperty("permissions");
        Assert.True(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.True(perms.GetProperty("canManageBills").GetBoolean());

        var paid = board.GetProperty("paid").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(0, paid.GetProperty("preparationDone").GetInt32());
        Assert.Equal(2, paid.GetProperty("preparationTotal").GetInt32());
        Assert.False(paid.GetProperty("allItemsPrepared").GetBoolean());

        var item = paid.GetProperty("items").EnumerateArray().First();
        Assert.True(item.TryGetProperty("id", out _));
        Assert.Equal(0, item.GetProperty("preparedQuantity").GetInt32());
        Assert.False(item.GetProperty("isPrepared").GetBoolean());
    }

    [Fact]
    public async Task Checkout_board_has_mark_paid_only_permissions()
    {
        var checkout = CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var perms = board.GetProperty("permissions");
        Assert.True(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.False(perms.GetProperty("canComplete").GetBoolean());
        Assert.False(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.False(perms.GetProperty("canManageBills").GetBoolean());
    }

    private async Task PrepareAllItemsAsync(HttpClient client, Guid orderId)
    {
        var ids = await GetItemIdsAsync(orderId);
        foreach (var itemId in ids)
        {
            var res = await client.PostAsJsonAsync(
                $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
                new { isPrepared = true });
            res.EnsureSuccessStatusCode();
        }
    }

    private async Task<Guid> GetFirstItemIdAsync(Guid orderId)
    {
        var ids = await GetItemIdsAsync(orderId);
        return ids[0];
    }

    private async Task<IReadOnlyList<Guid>> GetItemIdsAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToListAsync();
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
        string idemKey,
        int quantity = 1)
    {
        var payload = new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = idemKey,
            items = new[] { new { menuItemId = fixture.MenuItemId, quantity, notes = (string?)null } }
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(payload) };
        req.Headers.Add("Idempotency-Key", idemKey);
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
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
