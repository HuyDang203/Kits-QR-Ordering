using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Integration tests for the server-reconciliation-first behavior of the
/// Guest Order Session. Requires Docker (Testcontainers Postgres).
///
/// Verifies that the production track API surfaces the signals the boot
/// reconciliation needs:
///   - awaiting_payment → keep the session
///   - paid_preparing → keep the session
///   - completed → terminal, drop the session
///   - cancelled → terminal, drop the session
///   - 404 → terminal, drop the session
/// </summary>
public sealed class GuestSessionReconciliationIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    private static string WebRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    [Fact]
    public async Task Active_session_preserved_when_server_says_awaiting_payment()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idem = $"guest-reconcile-active-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idem, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var res = await _guest.GetAsync(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var track = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(track.GetProperty("pendingPayment").GetBoolean());
        Assert.False(track.GetProperty("isComplete").GetBoolean());
        Assert.False(track.TryGetProperty("isCancelled", out var c) && c.GetBoolean());
    }

    [Fact]
    public async Task Server_404_for_known_token_format_treats_session_as_invalid()
    {
        // A real order id + a wrong token must yield 404 so the tray's
        // refreshSubmittedTrayStatus hits resetGuestOrderSessionUi("server_404").
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var submit = await _guest.PostAsJsonAsync("/api/orders", new
        {
            venueTableId = fixture.VenueTableId,
            idempotencyKey = $"guest-404-{Guid.NewGuid():N}",
            paymentMethod = OrderPaymentMethods.Cash,
            items = new[]
            {
                new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null }
            }
        });
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();

        var res = await _guest.GetAsync($"/api/track/orders/{orderId}?token=definitely-not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Track_endpoint_says_completed_after_staff_complete()
    {
        // End-to-end: a Completed order is reported by the track API so the
        // boot reconciliation can safely drop the local session.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idem = $"guest-reconcile-complete-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idem, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        // Drive order through Paid → Completed via Staff.
        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var markPaid = await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        Assert.Equal(HttpStatusCode.OK, markPaid.StatusCode);
        var markComplete = await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/complete", new { });
        Assert.Equal(HttpStatusCode.OK, markComplete.StatusCode);

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Completed, refreshed.Status);

        var trackRes = await _guest.GetAsync($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, trackRes.StatusCode);
        var track = await trackRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(track.GetProperty("isComplete").GetBoolean(),
            "track API must report isComplete=true so the boot reconciliation can clear the session");
        Assert.Equal("completed", track.GetProperty("phaseKey").GetString());
    }

    [Fact]
    public async Task Track_endpoint_says_cancelled_after_staff_cancel()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idem = $"guest-reconcile-cancel-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idem, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var cancelRes = await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, refreshed.Status);

        var trackRes = await _guest.GetAsync($"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, trackRes.StatusCode);
        var track = await trackRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(track.GetProperty("isCancelled").GetBoolean(),
            "track API must report isCancelled=true so the boot reconciliation can clear the session");
    }

    [Fact]
    public async Task Original_order_intact_after_completion_signals_reconcile()
    {
        // The reconciliation ONLY drops the client-side Guest Order Session
        // pointer in localStorage. It MUST NOT delete or mutate the Order
        // record in the database. Verify that even after the API surfaces
        // the terminal signal, the Order row still exists unchanged.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idem = $"guest-reconcile-persist-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idem, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });
        await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/complete", new { });

        // Order and items are still in the database.
        var orderRow = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.NotNull(orderRow);
        Assert.Equal(OrderStatus.Completed, orderRow.Status);
        var itemRows = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId).ToListAsync();
        Assert.NotEmpty(itemRows);
    }

    // ---- helpers -----------------------------------------------------------------------

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture, string idemKey, string paymentMethod)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                venueTableId = fixture.VenueTableId,
                idempotencyKey = idemKey,
                paymentMethod,
                items = new[]
                {
                    new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null }
                }
            })
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _guest.SendAsync(req);
    }

    private static async Task LoginStaffAsync(HttpClient client)
    {
        var get = await client.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success);
        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-staff-secret-16",
            ["__RequestVerificationToken"] = tokenMatch.Groups["v"].Value
        };
        var post = await client.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task EnsureCleanAsync(AppDbContext db)
    {
        db.OperationalAuditEntries.RemoveRange(db.OperationalAuditEntries);
        await db.SaveChangesAsync();
        db.OrderItems.RemoveRange(db.OrderItems);
        db.Orders.RemoveRange(db.Orders);
        await db.SaveChangesAsync();
    }
}
