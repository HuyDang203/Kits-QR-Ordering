using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class StaffOrderStatusIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Forward_only_status_transitions_succeed_and_backward_transitions_are_rejected()
    {
        var staffClient = factory.CreateClient();
        await LoginStaffAsync(staffClient);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        var idemKey = "staff-forward-1";
        var orderId = await SubmitOrderAsync(staffClient, fixture, idemKey, quantity: 1);

        Assert.Equal(HttpStatusCode.OK, (await staffClient.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        // Paid → InProgress (legacy admin prep)
        var r1 = await PatchStatusAsync(staffClient, orderId, "preparing");
        Assert.Equal(HttpStatusCode.OK, r1.statusCode);
        Assert.Equal("preparing", r1.body.GetProperty("staffStatus").GetString());

        // InProgress → FinishingTouches
        var r2 = await PatchStatusAsync(staffClient, orderId, "finishing");
        Assert.Equal(HttpStatusCode.OK, r2.statusCode);
        Assert.Equal("finishing", r2.body.GetProperty("staffStatus").GetString());

        // FinishingTouches → Ready
        var r3 = await PatchStatusAsync(staffClient, orderId, "ready");
        Assert.Equal(HttpStatusCode.OK, r3.statusCode);
        Assert.Equal("ready", r3.body.GetProperty("staffStatus").GetString());

        // Ready → Completed via workflow endpoint (legacy PATCH to served is blocked)
        await PrepareAllItemsForOrderAsync(staffClient, orderId);
        var r4 = await staffClient.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, r4.StatusCode);

        // Completed → InProgress (backward) should fail
        var r5 = await PatchStatusAsync(staffClient, orderId, "preparing");
        Assert.Equal(HttpStatusCode.BadRequest, r5.statusCode);

        // Completed → Completed should still work (no-op via complete endpoint)
        var r6 = await staffClient.PostAsync($"/api/staff/orders/{orderId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, r6.StatusCode);
    }

    [Fact]
    public async Task Backward_transitions_from_InProgress_and_Ready_return_400()
    {
        var staffClient = factory.CreateClient();
        await LoginStaffAsync(staffClient);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        var inProgressOrderId = await SubmitOrderAsync(staffClient, fixture, "staff-back-inprogress-1", quantity: 1);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.PostAsJsonAsync($"/api/staff/orders/{inProgressOrderId}/mark-paid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(staffClient, inProgressOrderId, "preparing")).statusCode);

        var fromInProgress = await PatchStatusAsync(staffClient, inProgressOrderId, "pending");
        Assert.Equal(HttpStatusCode.BadRequest, fromInProgress.statusCode);

        var readyOrderId = await SubmitOrderAsync(staffClient, fixture, "staff-back-ready-1", quantity: 1);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.PostAsJsonAsync($"/api/staff/orders/{readyOrderId}/mark-paid", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(staffClient, readyOrderId, "preparing")).statusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(staffClient, readyOrderId, "finishing")).statusCode);
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(staffClient, readyOrderId, "ready")).statusCode);

        var fromReady = await PatchStatusAsync(staffClient, readyOrderId, "pending");
        Assert.Equal(HttpStatusCode.BadRequest, fromReady.statusCode);
    }

    [Fact]
    public async Task Invalid_staff_status_value_is_rejected()
    {
        var staffClient = factory.CreateClient();
        await LoginStaffAsync(staffClient);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = "staff-invalid-1";
        var orderId = await SubmitOrderAsync(staffClient, fixture, idemKey, quantity: 1);

        var res = await PatchStatusRawAsync(staffClient, orderId, "not-a-real-status");
        Assert.Equal(HttpStatusCode.BadRequest, res.statusCode);
        Assert.True(res.body.TryGetProperty("error", out var err));
    }

    [Fact]
    public async Task Staff_board_api_returns_operational_ticket_fields_with_snapshot_and_notes()
    {
        var staffClient = factory.CreateClient();
        await LoginStaffAsync(staffClient);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var idemKey = "staff-board-snapshot-1";

        var expectedTableCode = await db.VenueTables
            .Where(t => t.Id == fixture.VenueTableId)
            .Select(t => t.DisplayCode)
            .SingleAsync();

        var notes = "Guest: Alice";
        var orderId = await SubmitOrderAsync(staffClient, fixture, idemKey, quantity: 2, notes: notes);

        // Rename the menu item after submit; staff board must still show snapshot name.
        var menu = await db.MenuItems.FirstAsync(m => m.Id == fixture.MenuItemId);
        menu.Name = "Test Latte (Renamed)";
        await db.SaveChangesAsync();

        var board = await staffClient.GetAsync("/api/staff/orders");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);

        var boardBody = await board.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, boardBody.ValueKind);

        var active = GetProp(boardBody, "active");
        var orderEl = active.EnumerateArray().FirstOrDefault(x => GetProp(x, "id").GetGuid() == orderId);
        Assert.NotEqual(JsonValueKind.Undefined, orderEl.ValueKind);

        Assert.Equal(expectedTableCode, GetProp(orderEl, "tableCode").GetString());
        Assert.Equal("submitted", GetProp(orderEl, "staffStatus").GetString());
        Assert.True(GetProp(orderEl, "createdAtUtc").ValueKind is JsonValueKind.String);
        Assert.Equal(2, GetProp(orderEl, "totalCups").GetInt32());

        var items = GetProp(orderEl, "items");
        var item = items.EnumerateArray().First(i => GetProp(i, "menuItemId").GetGuid() == fixture.MenuItemId);

        Assert.Equal(fixture.MenuItemName, GetProp(item, "name").GetString());
        Assert.Equal(2, GetProp(item, "quantity").GetInt32());

        // Item-level notes
        Assert.Equal(notes, GetProp(item, "notes").GetString());

        // Ticket-level notes string (menu item name snapshot + notes)
        var guestNotes = GetProp(orderEl, "guestNotes").GetString();
        Assert.Contains(fixture.MenuItemName, guestNotes);
        Assert.Contains(notes, guestNotes);
    }

    private static JsonElement GetProp(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var p))
            return p;

        foreach (var objProp in el.EnumerateObject())
        {
            if (string.Equals(objProp.Name, propName, StringComparison.OrdinalIgnoreCase))
                return objProp.Value;
        }

        throw new InvalidOperationException($"Property '{propName}' not found in JSON object.");
    }

    private async Task LoginStaffAsync(HttpClient staffClient)
    {
        // Minimal Razor Pages antiforgery login.
        var get = await staffClient.GetAsync("/Staff/Login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (!tokenMatch.Success)
            throw new InvalidOperationException("Could not extract __RequestVerificationToken from staff login page.");

        var token = tokenMatch.Groups["v"].Value;

        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-staff-secret-16",
            ["__RequestVerificationToken"] = token
        };

        var post = await staffClient.PostAsync("/Staff/Login", new FormUrlEncodedContent(form!));
        // Redirect is expected; cookie must be set so authenticated calls succeed.
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
    }

    private static async Task<(HttpStatusCode statusCode, JsonElement body)> PatchStatusAsync(
        HttpClient client,
        Guid orderId,
        string staffStatus)
    {
        var req = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            $"/api/staff/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { staffStatus })
        };

        var res = await client.SendAsync(req);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (res.StatusCode, json);
    }

    private static async Task<(HttpStatusCode statusCode, JsonElement body)> PatchStatusRawAsync(
        HttpClient client,
        Guid orderId,
        string staffStatus)
    {
        var req = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            $"/api/staff/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { staffStatus })
        };

        var res = await client.SendAsync(req);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (res.StatusCode, json);
    }

    private static async Task<Guid> SubmitOrderAsync(
        HttpClient client,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        int quantity,
        string? notes = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["items"] = new[]
            {
                new
                {
                    menuItemId = fixture.MenuItemId,
                    quantity = quantity,
                    notes = notes
                }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("Idempotency-Key", idemKey);

        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
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

