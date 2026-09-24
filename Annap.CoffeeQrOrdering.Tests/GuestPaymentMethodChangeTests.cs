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

public sealed class GuestPaymentMethodChangeTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Cash_to_BankTransfer_updates_same_order_in_place()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-cash-to-bank-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var res = await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var changeBody = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, changeBody.GetProperty("orderId").GetGuid());
        Assert.Equal(OrderPaymentMethods.BankTransfer, changeBody.GetProperty("paymentMethod").GetString());
        Assert.False(changeBody.GetProperty("replay").GetBoolean());

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.BankTransfer, refreshed.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, refreshed.Status);
        Assert.NotNull(refreshed.BillNumber);
        Assert.Equal(1, await db.Orders.CountAsync(o => o.GuestSessionToken == token));
        // Items + totals unchanged.
        Assert.Equal(fixture.MenuPrice, refreshed.TotalAmount);
        var itemCount = await db.OrderItems.CountAsync(i => i.OrderId == orderId);
        Assert.Equal(1, itemCount);

        // OperationalAudit should have exactly one entry for the change.
        var auditEntries = await db.OperationalAuditEntries
            .Where(a => a.OrderId == orderId && a.ActionKind == "order.change_payment_method")
            .ToListAsync();
        Assert.Single(auditEntries);
        Assert.Contains("by=guest", auditEntries[0].Summary);

        // Staff board reports the new payment method.
        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(OrderPaymentMethods.BankTransfer, submitted.GetProperty("paymentMethod").GetString());
        Assert.Equal("Chuyển khoản", submitted.GetProperty("paymentMethodLabelVi").GetString());
    }

    [Fact]
    public async Task BankTransfer_to_Cash_preserves_BillNumber_and_order_id()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-bank-to-cash-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.BankTransfer);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var original = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.NotNull(original.BillNumber);

        var res = await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.Cash);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
        Assert.Equal(OrderStatus.Submitted, refreshed.Status);
        Assert.Equal(original.BillNumber, refreshed.BillNumber);
        Assert.Equal(1, await db.Orders.CountAsync(o => o.GuestSessionToken == token));
    }

    [Fact]
    public async Task Same_method_replay_returns_success_no_audit_row()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-replay-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var res = await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.Cash);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var changeBody = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(changeBody.GetProperty("replay").GetBoolean());

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);

        var auditEntries = await db.OperationalAuditEntries
            .Where(a => a.OrderId == orderId && a.ActionKind == "order.change_payment_method")
            .CountAsync();
        Assert.Equal(0, auditEntries);
    }

    [Fact]
    public async Task Change_after_mark_paid_is_rejected_and_payment_method_unchanged()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-after-paid-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var res = await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errBody = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("payment_method_locked", errBody.GetProperty("error").GetString());

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
        Assert.Equal(OrderStatus.Paid, refreshed.Status);
    }

    [Fact]
    public async Task Change_after_completed_is_rejected()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-after-completed-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
        await MarkAllItemsPreparedAsync(staff, orderId);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/api/staff/orders/{orderId}/complete", null)).StatusCode);

        var res = await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errBody = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("payment_method_locked", errBody.GetProperty("error").GetString());

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Completed, refreshed.Status);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
    }

    [Fact]
    public async Task Change_with_wrong_token_returns_404()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-wrong-token-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();

        var res = await ChangePaymentMethodAsync(orderId, "not-a-real-token", OrderPaymentMethods.BankTransfer);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
    }

    [Fact]
    public async Task Change_with_invalid_payment_method_returns_400()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-invalid-pm-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var res = await ChangePaymentMethodAsync(orderId, token, "NotARealMethod");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
    }

    [Fact]
    public async Task Concurrent_change_vs_mark_paid_never_silently_overwrites_paid_order()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-race-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        // Fire both requests in parallel.
        var guestChangeTask = ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer);

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var staffMarkPaidTask = staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { });

        await Task.WhenAll(guestChangeTask, staffMarkPaidTask);

        var changeResponse = await guestChangeTask;
        var markPaidResponse = await staffMarkPaidTask;
        var markPaidOk = markPaidResponse.StatusCode == HttpStatusCode.OK;

        var refreshed = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);

        // MarkPaid must never see PaymentMethod silently flipped to BankTransfer by a
        // concurrent change after the order is already Paid.
        if (refreshed.Status == OrderStatus.Paid)
        {
            // The order is Paid: PaymentMethod must equal what it was at the time of mark_paid.
            // If the change ran first and committed before mark_paid, both succeeded; mark_paid
            // never overwrites an existing PaymentMethod so this is consistent.
            // If the change tried AFTER mark_paid committed, it must have been rejected (Locked).
            if (refreshed.PaymentMethod == OrderPaymentMethods.BankTransfer)
            {
                // Change committed before mark_paid — both succeeded, no silent overwrite of Paid.
                Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, markPaidResponse.StatusCode);
            }
            else
            {
                // mark_paid committed before the change attempt. PaymentMethod must remain the
                // original Cash (or what the staff overrode), and the change request must have
                // been rejected with a non-200 status (locked or serialization conflict).
                Assert.Equal(OrderPaymentMethods.Cash, refreshed.PaymentMethod);
                Assert.NotEqual(HttpStatusCode.OK, changeResponse.StatusCode);
            }
        }
        else
        {
            // Order is still Submitted (change won, mark_paid never got the lock).
            // PaymentMethod may have flipped to BankTransfer; mark_paid should report failure.
            Assert.Equal(OrderStatus.Submitted, refreshed.Status);
            Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, markPaidResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Cash_to_BankTransfer_makes_transfer_qr_available_for_same_order()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-qr-available-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        // Before the change: transfer-qr endpoint reports it isn't a transfer order.
        var before = await _guest.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.False(before.GetProperty("enabled").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer)).StatusCode);

        // After the change: transfer-qr endpoint reports enabled with QR image URL.
        var after = await _guest.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.True(after.GetProperty("enabled").GetBoolean());
        Assert.True(after.TryGetProperty("qrImageUrl", out var qr) && qr.ValueKind == JsonValueKind.String);
        Assert.False(string.IsNullOrWhiteSpace(qr.GetString()));
    }

    [Fact]
    public async Task BankTransfer_to_Cash_makes_transfer_qr_unavailable_again()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-qr-disabled-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.BankTransfer);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var before = await _guest.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.True(before.GetProperty("enabled").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.Cash)).StatusCode);

        var after = await _guest.GetFromJsonAsync<JsonElement>(
            $"/api/orders/{orderId}/transfer-qr?token={Uri.EscapeDataString(token)}");
        Assert.False(after.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Staff_board_reports_updated_payment_method_after_guest_change()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-staff-board-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var boardBefore = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var rowBefore = boardBefore.GetProperty("submitted").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(OrderPaymentMethods.Cash, rowBefore.GetProperty("paymentMethod").GetString());
        Assert.Equal("Tiền mặt · chờ thanh toán", rowBefore.GetProperty("pendingPaymentBadgeVi").GetString());

        Assert.Equal(HttpStatusCode.OK, (await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer)).StatusCode);

        var boardAfter = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var rowAfter = boardAfter.GetProperty("submitted").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(OrderPaymentMethods.BankTransfer, rowAfter.GetProperty("paymentMethod").GetString());
        Assert.Equal("Chuyển khoản", rowAfter.GetProperty("paymentMethodLabelVi").GetString());
        Assert.Equal("Chuyển khoản · chờ xác nhận", rowAfter.GetProperty("pendingPaymentBadgeVi").GetString());
    }

    [Fact]
    public async Task Guest_session_after_change_keeps_orderId_token_table_intact()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureCleanAsync(db);

        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = $"gpc-session-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(fixture, idemKey, OrderPaymentMethods.Cash);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await ChangePaymentMethodAsync(orderId, token, OrderPaymentMethods.BankTransfer)).StatusCode);

        // Server-side: only PaymentMethod may change; everything else is preserved.
        var refreshed = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
        Assert.Equal(token, refreshed.GuestSessionToken);
        Assert.Equal(fixture.VenueTableId, refreshed.VenueTableId);
        Assert.NotNull(refreshed.BillNumber);
        Assert.Single(refreshed.Items);
        Assert.Equal(OrderStatus.Submitted, refreshed.Status);

        // /api/track/orders/{id}?token=... still works (same order, same token).
        var track = await _guest.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(track.GetProperty("pendingPayment").GetBoolean());
        Assert.Equal(OrderPaymentMethods.BankTransfer, track.GetProperty("paymentMethod").GetString());
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["paymentMethod"] = paymentMethod,
            ["fulfillmentMode"] = "DineIn",
            ["items"] = new[]
            {
                new { menuItemId = fixture.MenuItemId, quantity = 1, notes = (string?)null }
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return await _guest.SendAsync(req);
    }

    private async Task<HttpResponseMessage> ChangePaymentMethodAsync(
        Guid orderId,
        string token,
        string paymentMethod)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payment-method")
        {
            Content = JsonContent.Create(new { paymentMethod, token })
        };
        return await _guest.SendAsync(req);
    }

    private async Task MarkAllItemsPreparedAsync(HttpClient staff, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var itemIds = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.Id)
            .ToListAsync();
        foreach (var itemId in itemIds)
        {
            var res = await staff.PostAsJsonAsync(
                $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
                new { isPrepared = true });
            res.EnsureSuccessStatusCode();
        }
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
        // Strip pre-existing orders so per-test OrderIds / tokens are predictable.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"order_items\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"operational_audit_entries\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"orders\"");
    }
}
