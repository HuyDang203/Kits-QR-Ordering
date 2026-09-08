using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestPaymentCelebrationIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Guest_track_api_pending_then_paid_after_staff_mark_paid()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var guest = factory.CreateClient();
        var idem = $"guest-paid-poll-{Guid.NewGuid():N}";
        var submit = await PostOrderAsync(guest, fixture, idem, OrderPaymentMethods.BankTransfer);
        submit.EnsureSuccessStatusCode();
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("id").GetGuid();
        var token = body.GetProperty("guestSessionToken").GetString()!;

        var pending = await guest.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.True(pending.GetProperty("pendingPayment").GetBoolean());
        Assert.False(pending.GetProperty("showBill").GetBoolean());

        var staff = factory.CreateClient();
        await LoginStaffAsync(staff);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/staff/orders/{orderId}/mark-paid", new { })).StatusCode);

        var paid = await guest.GetFromJsonAsync<JsonElement>(
            $"/api/track/orders/{orderId}?token={Uri.EscapeDataString(token)}");
        Assert.False(paid.GetProperty("pendingPayment").GetBoolean());
        Assert.True(paid.GetProperty("showBill").GetBoolean());
        Assert.Equal("paid_preparing", paid.GetProperty("phaseKey").GetString());
        Assert.False(string.IsNullOrWhiteSpace(paid.GetProperty("paidAtUtc").GetString()));
    }

    [Fact]
    public async Task Guest_track_api_invalid_token_returns_404()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);
        var guest = factory.CreateClient();
        var submit = await PostOrderAsync(guest, fixture, $"guest-bad-token-{Guid.NewGuid():N}", OrderPaymentMethods.Cash);
        var orderId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var res = await guest.GetAsync($"/api/track/orders/{orderId}?token=not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostOrderAsync(
        HttpClient guest,
        OrderTestSeedHelper.OrderSubmitFixture fixture,
        string idemKey,
        string paymentMethod)
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
        return await guest.SendAsync(req);
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
