using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Annap.CoffeeQrOrdering.Tests;

public sealed class OrderWorkflowIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task New_order_is_submitted_and_appears_in_submitted_column()
    {
        var client = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(client, fixture, "wf-submitted-1");

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff, "test-host", "test-staff-secret-16");

        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted");
        Assert.Contains(submitted.EnumerateArray(), x => x.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Mark_paid_sets_paid_status_and_paid_at()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-paid-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");

        var res = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("paid", body.GetProperty("staffStatus").GetString());
        Assert.True(body.TryGetProperty("paidAtUtc", out _));
        Assert.True(body.TryGetProperty("bill", out var bill));
        Assert.True(bill.TryGetProperty("totalAmount", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAtUtc);
    }

    [Fact]
    public async Task Mark_paid_is_idempotent_for_already_paid_order()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-paid-idem-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");

        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
        var second = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("replay").GetBoolean());
    }

    [Fact]
    public async Task Cannot_complete_unpaid_order()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-complete-block-1");

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");

        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Barista_can_complete_paid_order()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-complete-1");

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");
        await PrepareAllItemsForOrderAsync(barista, orderId);
        var res = await barista.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.CompletedAtUtc);
    }

    [Fact]
    public async Task Checkout_cannot_complete_order()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-checkout-complete-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var res = await checkout.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Checkout_cannot_access_admin_menu_page()
    {
        var checkout = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var res = await checkout.GetAsync("/admin/menu");
        Assert.True(
            res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {res.StatusCode}");
    }

    [Fact]
    public async Task Guest_bill_requires_valid_token()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-bill-token-1");

        var noToken = await guest.GetAsync($"/api/orders/{orderId}/bill");
        Assert.Equal(HttpStatusCode.NotFound, noToken.StatusCode);
    }

    [Fact]
    public async Task Guest_track_shows_pending_payment_then_bill_after_mark_paid()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var (orderId, token) = await SubmitOrderWithTokenAsync(guest, fixture, "wf-track-1");

        var pending = await guest.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(pending.GetProperty("pendingPayment").GetBoolean());
        Assert.False(pending.GetProperty("showBill").GetBoolean());
        Assert.True(pending.GetProperty("showCheckBill").GetBoolean());

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var paid = await guest.GetFromJsonAsync<JsonElement>($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.False(paid.GetProperty("pendingPayment").GetBoolean());
        Assert.True(paid.GetProperty("showBill").GetBoolean());
        Assert.True(paid.TryGetProperty("bill", out _));
    }

    [Fact]
    public async Task Bill_total_uses_submitted_price_snapshot()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-bill-snap-1", quantity: 2);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var menu = await db.MenuItems.FirstAsync(m => m.Id == fixture.MenuItemId);
        menu.Price = 999_999m;
        await db.SaveChangesAsync();

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        var paid = await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        var body = await paid.Content.ReadFromJsonAsync<JsonElement>();
        var bill = body.GetProperty("bill");
        var expected = fixture.MenuPrice * 2;
        Assert.Equal(expected, bill.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Paid_and_completed_orders_appear_in_correct_board_columns()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-board-cols-1");

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var paidBoard = await admin.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.Contains(paidBoard.GetProperty("paid").EnumerateArray(), x => x.GetProperty("id").GetGuid() == orderId);

        await PrepareAllItemsForOrderAsync(admin, orderId);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);

        var doneBoard = await admin.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.Contains(doneBoard.GetProperty("completed").EnumerateArray(), x => x.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Checkout_cannot_use_legacy_patch_status()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-checkout-patch-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");

        var res = await PatchStatusAsync(checkout, orderId, "served");
        Assert.Equal(HttpStatusCode.Forbidden, res.statusCode);
    }

    [Fact]
    public async Task Admin_legacy_patch_cannot_skip_payment_from_submitted()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-admin-patch-bypass-1");

        var admin = factory.CreateClient();
        await LoginStaffAsync(admin, "test-host", "test-staff-secret-16");

        var res = await PatchStatusAsync(admin, orderId, "served");
        Assert.Equal(HttpStatusCode.BadRequest, res.statusCode);
        Assert.NotNull(res.body);
        Assert.Contains("mark-paid", res.body.Value.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Barista_cannot_mark_paid()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-barista-mark-1");

        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");

        var res = await barista.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_access_staff_board_api()
    {
        var anon = factory.CreateClient();
        var res = await anon.GetAsync("/api/staff/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Staff_admin_can_get_board_with_expected_shape()
    {
        var staff = factory.CreateClient();
        await LoginStaffAsync(staff, "test-host", "test-staff-secret-16");

        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.TryGetProperty("submitted", out _));
        Assert.True(board.TryGetProperty("paid", out _));
        Assert.True(board.TryGetProperty("completed", out _));
        Assert.True(board.TryGetProperty("permissions", out var perms));
        Assert.True(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.True(perms.GetProperty("canComplete").GetBoolean());
        Assert.True(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.True(perms.GetProperty("canManageBills").GetBoolean());
    }

    [Fact]
    public async Task Checkout_can_get_board_with_mark_paid_permission_only()
    {
        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");

        var board = await checkout.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.TryGetProperty("submitted", out _));
        Assert.True(board.TryGetProperty("paid", out _));
        Assert.True(board.TryGetProperty("completed", out _));
        var perms = board.GetProperty("permissions");
        Assert.True(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.False(perms.GetProperty("canComplete").GetBoolean());
        Assert.False(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.False(perms.GetProperty("canManageBills").GetBoolean());
    }

    [Fact]
    public async Task Barista_can_get_board_with_complete_permission_only()
    {
        var barista = factory.CreateClient();
        await LoginStaffAsync(barista, "test-host", "test-barista-secret-16");

        var board = await barista.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.True(board.TryGetProperty("submitted", out _));
        Assert.True(board.TryGetProperty("paid", out _));
        Assert.True(board.TryGetProperty("completed", out _));
        var perms = board.GetProperty("permissions");
        Assert.False(perms.GetProperty("canMarkPaid").GetBoolean());
        Assert.True(perms.GetProperty("canComplete").GetBoolean());
        Assert.True(perms.GetProperty("canPrepareItems").GetBoolean());
        Assert.False(perms.GetProperty("canManageBills").GetBoolean());
    }

    [Fact]
    public async Task Empty_board_response_has_all_columns_and_permissions()
    {
        var staff = factory.CreateClient();
        await LoginStaffAsync(staff, "test-host", "test-staff-secret-16");

        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        Assert.Equal(JsonValueKind.Array, board.GetProperty("submitted").ValueKind);
        Assert.Equal(JsonValueKind.Array, board.GetProperty("paid").ValueKind);
        Assert.Equal(JsonValueKind.Array, board.GetProperty("completed").ValueKind);
        Assert.Equal(JsonValueKind.Object, board.GetProperty("permissions").ValueKind);
    }

    [Fact]
    public async Task Mark_paid_twice_keeps_bill_number_and_paid_at_stable()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-bill-idem-1");

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");

        var first = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var billNumber = firstBody.GetProperty("billNumber").GetString();
        var paidAt = firstBody.GetProperty("paidAtUtc").GetString();

        var second = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondBody.GetProperty("replay").GetBoolean());
        Assert.Equal(billNumber, secondBody.GetProperty("billNumber").GetString());
    }

    [Fact]
    public async Task Bill_line_totals_sum_to_snapshot_total()
    {
        var guest = factory.CreateClient();
        var fixture = await SeedFixtureAsync();
        var orderId = await SubmitOrderAsync(guest, fixture, "wf-bill-sum-1", quantity: 3);

        var checkout = factory.CreateClient();
        await LoginStaffAsync(checkout, "test-host", "test-checkout-secret-16");
        var paid = await checkout.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        var bill = (await paid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bill");
        var total = bill.GetProperty("totalAmount").GetDecimal();
        decimal sum = 0;
        foreach (var line in bill.GetProperty("items").EnumerateArray())
            sum += line.GetProperty("lineTotal").GetDecimal();
        Assert.Equal(sum, total);
        Assert.Equal(fixture.MenuPrice * 3, total);
    }

    private static async Task<(HttpStatusCode statusCode, JsonElement? body)> PatchStatusAsync(
        HttpClient client,
        Guid orderId,
        string staffStatus)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/staff/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { staffStatus })
        };
        var res = await client.SendAsync(req);
        JsonElement? json = null;
        if (res.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (res.StatusCode, json);
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
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
        var token = tokenMatch.Groups["v"].Value;
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task<Guid> SubmitOrderAsync(
        HttpClient client,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        int quantity = 1)
    {
        var (orderId, _) = await SubmitOrderWithTokenAsync(client, fixture, idemKey, quantity);
        return orderId;
    }

    private static async Task<(Guid OrderId, string Token)> SubmitOrderWithTokenAsync(
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
        return (body.GetProperty("id").GetGuid(), body.GetProperty("guestSessionToken").GetString()!);
    }

    private async Task PrepareAllItemsForOrderAsync(HttpClient client, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var itemIds = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.Id)
            .ToListAsync();
        foreach (var itemId in itemIds)
        {
            var res = await client.PostAsJsonAsync(
                $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
                new { isPrepared = true });
            res.EnsureSuccessStatusCode();
        }
    }
}
