using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ItemCustomerNoteTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Vietnamese_item_note_persists_correctly()
    {
        var fixture = await SeedFixtureAsync();
        const string note = "ít sữa";

        var res = await PostOrderAsync(fixture, "item-note-vi-1", customerNote: note);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == orderId);
        Assert.Equal(note, item.CustomerNote);
    }

    [Fact]
    public async Task Staff_board_paid_and_completed_columns_return_item_customer_note()
    {
        var fixture = await SeedFixtureAsync();
        const string note = "ít sữa";
        var idem = $"item-note-lifecycle-{Guid.NewGuid():N}";
        var res = await PostOrderAsync(fixture, idem, customerNote: note);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var paidBoard = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var paid = paidBoard.GetProperty("paid").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(note, paid.GetProperty("items").EnumerateArray().First().GetProperty("customerNote").GetString());

        var itemId = paid.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/items/{itemId}/prepared",
            new { preparedQuantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/complete", new { })).StatusCode);

        var doneBoard = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var completed = doneBoard.GetProperty("completed").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(note, completed.GetProperty("items").EnumerateArray().First().GetProperty("customerNote").GetString());
    }

    [Fact]
    public async Task Staff_bill_api_returns_item_customer_note_under_item()
    {
        var fixture = await SeedFixtureAsync();
        const string note = "không kem";
        var idem = $"item-note-bill-{Guid.NewGuid():N}";
        var res = await PostOrderAsync(fixture, idem, customerNote: note);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);
        var bill = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/orders/{orderId}/bill");
        var line = bill.GetProperty("items").EnumerateArray().First();
        Assert.Equal(note, line.GetProperty("customerNote").GetString());
    }

    [Theory]
    [InlineData(OrderPaymentMethods.Cash)]
    [InlineData(OrderPaymentMethods.Card)]
    [InlineData(OrderPaymentMethods.BankTransfer)]
    public async Task Item_note_survives_submit_for_each_payment_method(string paymentMethod)
    {
        var fixture = await SeedFixtureAsync();
        const string note = "ít sữa";
        var idem = $"item-note-pay-{paymentMethod}-{Guid.NewGuid():N}";
        var res = await PostOrderAsync(fixture, idem, customerNote: note, paymentMethod: paymentMethod);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(note, submitted.GetProperty("items").EnumerateArray().First().GetProperty("customerNote").GetString());
    }

    [Fact]
    public async Task Guest_submit_item_note_persists_on_order_item()
    {
        var fixture = await SeedFixtureAsync();
        const string note = "Ít đá";

        var res = await PostOrderAsync(fixture, "item-note-persist-1", customerNote: note);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == orderId);
        Assert.Equal(note, item.CustomerNote);
    }

    [Fact]
    public async Task Guest_submit_overlength_item_note_returns_400()
    {
        var fixture = await SeedFixtureAsync();
        var tooLong = new string('a', OrderItemCustomerNoteHelper.MaxLength + 1);
        var res = await PostOrderAsync(fixture, "item-note-too-long-1", customerNote: tooLong);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Staff_board_api_returns_item_customer_note()
    {
        var fixture = await SeedFixtureAsync();
        const string note = "Không kem";
        var idem = $"item-note-board-{Guid.NewGuid():N}";
        var res = await PostOrderAsync(fixture, idem, customerNote: note);
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        var board = await staff.GetFromJsonAsync<JsonElement>("/api/staff/orders");
        var submitted = board.GetProperty("submitted").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == orderId);
        var item = submitted.GetProperty("items").EnumerateArray().First();
        Assert.Equal(note, item.GetProperty("customerNote").GetString());
    }

    [Fact]
    public async Task Empty_item_notes_do_not_break_submit()
    {
        var fixture = await SeedFixtureAsync();
        var res = await PostOrderAsync(fixture, "item-note-empty-1", customerNote: "   ");
        res.EnsureSuccessStatusCode();
        var orderId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == orderId);
        Assert.True(string.IsNullOrWhiteSpace(item.CustomerNote));
    }

    private async Task<OrderTestSeedHelper.OrderSubmitFixture> SeedFixtureAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string? customerNote = null,
        int quantity = 1,
        string paymentMethod = OrderPaymentMethods.Cash)
    {
        var item = new Dictionary<string, object?>
        {
            ["menuItemId"] = fixture.MenuItemId,
            ["quantity"] = quantity,
            ["notes"] = null
        };
        if (customerNote is not null)
            item["customerNote"] = customerNote;

        var payload = new Dictionary<string, object?>
        {
            ["venueTableId"] = fixture.VenueTableId,
            ["idempotencyKey"] = idemKey,
            ["paymentMethod"] = paymentMethod,
            ["items"] = new object[] { item }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
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
}
