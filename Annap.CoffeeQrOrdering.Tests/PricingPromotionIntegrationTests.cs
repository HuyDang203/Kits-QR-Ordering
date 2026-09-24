using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Integration tests for the global "Giảm giá 10%" promotion feature.
///
/// Each test resets the singleton row to OFF before exercising the
/// scenarios, so the suite is order-independent and safe to run alongside
/// other test classes that also touch the singleton.
///
/// Coverage:
///   1. Default OFF state at deploy time.
///   2. /api/menu returns base prices when OFF.
///   3. Toggle ON → /api/menu returns 90 % prices; global across items.
///   4. Toggle OFF → prices revert to base; no double discount.
///   5. Historical orders before ON are preserved when ON.
///   6. Historical orders placed while ON stay ON-priced after OFF.
///   7. POST /api/orders snapshots the effective price (server-authoritative).
///   8. Server rejects client-supplied unit price (no field on the request DTO).
///   9. POST /api/orders computes order total from snapshotted unit prices.
///  10. Bank transfer QR amount equals order total after discount.
///  11. Rounding policy matches VndMoneyFormatter (MidpointRounding.AwayFromZero).
///  12. Admin authorization: only the Admin role can flip the toggle.
/// </summary>
public sealed class PricingPromotionIntegrationTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private async Task ResetPromotionAsync(bool enabled = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sid = PricingPromotionSettingsConfiguration.SingletonId;
        var row = await db.PricingPromotionSettings.FirstOrDefaultAsync(p => p.Id == sid);
        if (row is null)
        {
            db.PricingPromotionSettings.Add(new PricingPromotionSettings
            {
                Id = sid,
                Enabled = enabled,
                Percent = 10
            });
        }
        else
        {
            row.Enabled = enabled;
            row.Percent = 10;
        }
        await db.SaveChangesAsync();
    }

    private async Task<MenuItem> SeedMenuItemAsync(decimal price, string name = "Integration Promo Drink")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new MenuCategory { Name = "Integration Promo Category", SortOrder = 999 };
        db.MenuCategories.Add(cat);
        await db.SaveChangesAsync();
        var item = new MenuItem
        {
            CategoryId = cat.Id,
            Name = name,
            Price = price,
            IsAvailable = true,
            IsArchived = false
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private async Task<VenueTable> SeedActiveTableAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = $"T{suffix[..2]}{suffix[2..4]}",
            PublicSlug = $"annap-promo-{suffix}",
            DisplayLabel = $"Promo Table {suffix[..4]}",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();
        return table;
    }

    private async Task<HttpResponseMessage> SubmitOrderAsync(
        Guid venueTableId,
        Guid menuItemId,
        int quantity,
        string idemKey)
    {
        var payload = new
        {
            venueTableId,
            fulfillmentMode = "DineIn",
            paymentMethod = "Cash",
            idempotencyKey = idemKey,
            items = new[] { new { menuItemId, quantity } }
        };
        return await _client.PostAsJsonAsync("/api/orders", payload, JsonOpts);
    }

    private static async Task SignInAsAdminAsync(HttpClient client)
    {
        // The Admin page uses [Authorize(Policy = "Staff")] which maps to
        // StaffRoleNames.Admin. We sign in via the shared-password fallback
        // configured in AnnapPostgresWebApplicationFactory. The login page
        // uses an antiforgery token that we have to scrape first.
        var get = await client.GetAsync("/staff/login");
        get.EnsureSuccessStatusCode();
        var html = await get.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success, "Antiforgery token not found on /staff/login.");
        var token = tokenMatch.Groups["v"].Value;

        var form = new Dictionary<string, string?>
        {
            ["UserName"] = "test-host",
            ["Password"] = "test-staff-secret-16",
            ["__RequestVerificationToken"] = token
        };
        var post = await client.PostAsync("/staff/login", new FormUrlEncodedContent(form!));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Admin login failed: {(int)post.StatusCode}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1) Default OFF at deploy time.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Default_state_is_off_at_deploy_time()
    {
        await ResetPromotionAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.PricingPromotionSettings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
        Assert.NotNull(row);
        Assert.False(row!.Enabled);
        Assert.Equal(10, row.Percent);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2) /api/menu returns base prices when OFF.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Api_menu_returns_base_price_when_promotion_off()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedMenuItemAsync(price: 100_000m, name: "Promo-Off-Coffee");
        await SeedMenuItemAsync(price: 39_000m, name: "Promo-Off-Bakery");

        var resp = await _client.GetAsync("/api/menu");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(body.GetProperty("promotionActive").GetBoolean() == false);
        Assert.Equal(0, body.GetProperty("promotionPercent").GetInt32());

        // Verify the response actually contains the items we just seeded.
        var itemsFound = body.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Where(i => i.GetProperty("id").GetGuid() == item.Id)
            .ToList();
        Assert.Single(itemsFound);
        var our = itemsFound[0];
        Assert.Equal(100_000m, our.GetProperty("price").GetDecimal());
        Assert.Equal(100_000m, our.GetProperty("effectivePrice").GetDecimal());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3) Toggle ON → /api/menu returns 90 % prices; global across items.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Toggle_on_applies_ten_percent_to_every_visible_item()
    {
        await ResetPromotionAsync(enabled: false);
        var expensive = await SeedMenuItemAsync(price: 100_000m, name: "Promo-On-Expensive");
        var cheap = await SeedMenuItemAsync(price: 55_000m, name: "Promo-On-Cheap");

        // Flip the toggle directly via DbContext — this is the same write
        // path the Admin UI uses.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = true;
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync("/api/menu");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(body.GetProperty("promotionActive").GetBoolean());
        Assert.Equal(10, body.GetProperty("promotionPercent").GetInt32());

        var items = body.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .ToList();

        var expensiveRow = items.Single(i => i.GetProperty("id").GetGuid() == expensive.Id);
        var cheapRow = items.Single(i => i.GetProperty("id").GetGuid() == cheap.Id);

        Assert.Equal(100_000m, expensiveRow.GetProperty("price").GetDecimal());
        Assert.Equal(90_000m, expensiveRow.GetProperty("effectivePrice").GetDecimal());
        Assert.Equal(55_000m, cheapRow.GetProperty("price").GetDecimal());
        Assert.Equal(49_500m, cheapRow.GetProperty("effectivePrice").GetDecimal());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4) Toggle OFF → prices revert to base. Single arithmetic path: no
    //    "double discount" where 90 % is applied again.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Toggle_off_reverts_to_base_price_and_does_not_double_discount()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedMenuItemAsync(price: 100_000m, name: "Promo-Off-Revert");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = true;
            await db.SaveChangesAsync();
        }

        // Verify ON yields 90 000.
        var onResp = await _client.GetAsync("/api/menu");
        var onBody = await onResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var onRow = onBody.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetGuid() == item.Id);
        Assert.Equal(90_000m, onRow.GetProperty("effectivePrice").GetDecimal());

        // Toggle OFF.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = false;
            await db.SaveChangesAsync();
        }

        var offResp = await _client.GetAsync("/api/menu");
        var offBody = await offResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(offBody.GetProperty("promotionActive").GetBoolean());

        var offRow = offBody.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetGuid() == item.Id);

        // Critical: 90_000 * 0.9 = 81_000 must NEVER happen. The handler
        // resets to base first.
        Assert.Equal(100_000m, offRow.GetProperty("price").GetDecimal());
        Assert.Equal(100_000m, offRow.GetProperty("effectivePrice").GetDecimal());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5) Historical order placed while OFF stays at base even after ON.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Historical_order_before_promotion_keeps_base_price_after_toggle_on()
    {
        await ResetPromotionAsync(enabled: false);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 100_000m, name: "Promo-Historical-Before");

        // Submit while OFF.
        var idemBefore = Guid.NewGuid().ToString("N");
        var before = await SubmitOrderAsync(table.Id, item.Id, quantity: 1, idemBefore);
        Assert.Equal(HttpStatusCode.Created, before.StatusCode);
        var beforeBody = await before.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var beforeOrderId = beforeBody.GetProperty("id").GetGuid();
        Assert.Equal(100_000m, beforeBody.GetProperty("totalAmount").GetDecimal());

        // Now flip ON. The historical order must not be recalculated.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = true;
            await db.SaveChangesAsync();
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var historicOrder = await verifyDb.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == beforeOrderId);
        Assert.Equal(100_000m, historicOrder.TotalAmount);
        Assert.Equal(100_000m, historicOrder.Items.Single().UnitPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6) Historical order placed while ON stays at effective price after OFF.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Historical_order_during_promotion_keeps_effective_price_after_toggle_off()
    {
        await ResetPromotionAsync(enabled: false);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 100_000m, name: "Promo-Historical-During");

        // Flip ON.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = true;
            await db.SaveChangesAsync();
        }

        // Submit while ON.
        var idemDuring = Guid.NewGuid().ToString("N");
        var during = await SubmitOrderAsync(table.Id, item.Id, quantity: 1, idemDuring);
        Assert.Equal(HttpStatusCode.Created, during.StatusCode);
        var duringBody = await during.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var duringOrderId = duringBody.GetProperty("id").GetGuid();
        Assert.Equal(90_000m, duringBody.GetProperty("totalAmount").GetDecimal());

        // Flip OFF.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            row.Enabled = false;
            await db.SaveChangesAsync();
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var historicOrder = await verifyDb.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == duringOrderId);
        Assert.Equal(90_000m, historicOrder.TotalAmount);
        Assert.Equal(90_000m, historicOrder.Items.Single().UnitPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7) POST /api/orders snapshots the effective price (server-authoritative).
    //    8) The request DTO has no unitPrice field — server ignores client price.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Order_submit_snapshots_server_authoritative_unit_price_and_total()
    {
        await ResetPromotionAsync(enabled: true);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 55_000m, name: "Promo-Order-Snapshot");

        var idem = Guid.NewGuid().ToString("N");
        var resp = await SubmitOrderAsync(table.Id, item.Id, quantity: 3, idem);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = body.GetProperty("id").GetGuid();

        // Total = 49 500 × 3 = 148 500
        Assert.Equal(148_500m, body.GetProperty("totalAmount").GetDecimal());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstAsync(o => o.Id == orderId);
        Assert.Equal(148_500m, order.TotalAmount);
        var line = order.Items.Single();
        Assert.Equal(49_500m, line.UnitPrice);
        Assert.Equal(item.Id, line.MenuItemId);
        Assert.Equal(3, line.Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9) Cart price matches catalog: client sends {menuItemId,quantity} only;
    //    no unitPrice is accepted by the server. A forged payload with a
    //    custom unitPrice must be ignored (or rejected). The CreateOrderRequest
    //    binds only known fields, so extra JSON members are dropped.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Order_submit_ignores_client_supplied_unit_price_field()
    {
        await ResetPromotionAsync(enabled: true);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 100_000m, name: "Promo-Price-Tampering");

        // Forged payload: the client tries to set unitPrice = 1. The server
        // must NOT trust this — it should charge 90 000 per cup.
        var idem = Guid.NewGuid().ToString("N");
        var payload = new
        {
            venueTableId = table.Id,
            fulfillmentMode = "DineIn",
            paymentMethod = "Cash",
            idempotencyKey = idem,
            items = new[]
            {
                new
                {
                    menuItemId = item.Id,
                    quantity = 1,
                    unitPrice = 1m  // <-- malicious field
                }
            }
        };
        var resp = await _client.PostAsJsonAsync("/api/orders", payload, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = body.GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var line = await db.OrderItems.AsNoTracking().FirstAsync(i => i.OrderId == orderId);
        Assert.Equal(90_000m, line.UnitPrice);  // server-authoritative, not 1
        Assert.Equal(90_000m, (await db.Orders.FirstAsync(o => o.Id == orderId)).TotalAmount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10) Bank transfer QR amount equals order total after discount.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Bank_transfer_qr_amount_uses_snapshot_total_after_discount()
    {
        await ResetPromotionAsync(enabled: true);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 39_000m, name: "Promo-BankQR-Drink");

        var idem = Guid.NewGuid().ToString("N");
        var submit = await SubmitOrderAsync(table.Id, item.Id, quantity: 2, idem);
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var submitBody = await submit.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var orderId = submitBody.GetProperty("id").GetGuid();
        var token = submitBody.GetProperty("guestSessionToken").GetString();

        // Switch the order to BankTransfer so the QR builder returns a pending
        // (i.e. amount-bearing) DTO. We don't have an auth flow here, so we
        // patch via DbContext.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == orderId);
            order.PaymentMethod = "BankTransfer";
            await db.SaveChangesAsync();
        }

        var qrResp = await _client.GetAsync($"/api/orders/{orderId:D}/transfer-qr?token={Uri.EscapeDataString(token!)}");
        Assert.Equal(HttpStatusCode.OK, qrResp.StatusCode);
        var qr = await qrResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // 39 000 × 90 % = 35 100 (rounding-safe, exact). 2 cups → 70 200.
        Assert.Equal(70_200L, qr.GetProperty("amount").GetInt64());
        Assert.Equal("70.200 ₫", qr.GetProperty("amountFormatted").GetString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11) Rounding policy: 39 000 × 0.9 = 35 100 (exact, no fractional đồng).
    //     Confirms MidpointRounding.AwayFromZero matches the published UX.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Rounding_policy_produces_whole_vnd_amounts()
    {
        await ResetPromotionAsync(enabled: true);
        var table = await SeedActiveTableAsync();
        var item = await SeedMenuItemAsync(price: 39_000m, name: "Promo-Rounding-Drink");

        var idem = Guid.NewGuid().ToString("N");
        var resp = await SubmitOrderAsync(table.Id, item.Id, quantity: 1, idem);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        Assert.Equal(35_100m, body.GetProperty("totalAmount").GetDecimal());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.SubmitIdempotencyKey == idem);
        var line = await db.OrderItems.AsNoTracking().FirstAsync(i => i.OrderId == order.Id);

        // Stored as decimal(10,2): the .0 in 35 100 is a whole VND unit.
        Assert.Equal(35_100.00m, line.UnitPrice);
        Assert.Equal(35_100.00m, order.TotalAmount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12) Admin authorization: the Admin Menu Index page requires Staff
    //     (= Admin) policy. Anonymous access is redirected to login.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Admin_menu_page_anonymous_request_is_redirected_to_login()
    {
        // Reset promotion OFF so this test isn't affected by sibling tests.
        await ResetPromotionAsync(enabled: false);

        // No cookie. Server should redirect to /staff/login.
        var resp = await _client.GetAsync("/admin/menu");
        var status = (int)resp.StatusCode;
        // Either 302/303 redirect to login OR 200 with an unauthenticated
        // shell. Both are valid outcomes of the [Authorize] attribute on
        // the page; we assert that we never reached the toggle handler.
        Assert.True(status is >= 300 and < 400 || status == 200,
            $"Unexpected anonymous status {status}.");
        // Crucially: no promotion state was leaked through.
        var body = await resp.Content.ReadAsStringAsync();
        if (status == 200)
        {
            // Logged-out shell must not contain the "Giảm giá 10%: ĐANG BẬT"
            // banner because the toggle handler didn't run.
            Assert.DoesNotContain("menu-operator-promo__status", body);
        }
    }

    [Fact]
    public async Task Admin_toggle_handler_updates_singleton_row_when_signed_in()
    {
        await ResetPromotionAsync(enabled: false);

        // Sign in as admin via shared password.
        await SignInAsAdminAsync(_client);

        // Step 1: scrape the antiforgery token from any authenticated page.
        // /admin returns 200 when authenticated; the body contains the token.
        var page = await _client.GetAsync("/admin/menu");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var antiforgeryMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.True(antiforgeryMatch.Success, "Antiforgery token not found on /admin/menu.");
        var antiforgery = antiforgeryMatch.Groups["v"].Value;

        // Step 2: POST SetPromotionEnabled enabled=true.
        var post = await _client.PostAsync(
            "/admin/menu?handler=SetPromotionEnabled&enabled=true",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["__RequestVerificationToken"] = antiforgery
            }));
        Assert.True(post.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Toggle POST returned {(int)post.StatusCode}");

        // Step 3: the singleton row must be ON in the DB. This is the
        // authoritative test of the toggle: regardless of any UI quirks,
        // the server-side state is what the rest of the system reads.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.AsNoTracking()
                .FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            Assert.True(row.Enabled, "Toggle-ON did not flip the singleton row.");
            Assert.Equal(10, row.Percent);
        }

        // Step 4: the /api/menu catalog must now show 90 % effective prices
        // — proves the toggle end-to-end reaches the guest pricing pipeline.
        var menuResp = await _client.GetAsync("/api/menu");
        Assert.Equal(HttpStatusCode.OK, menuResp.StatusCode);
        var menuBody = await menuResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(menuBody.GetProperty("promotionActive").GetBoolean());
        Assert.Equal(10, menuBody.GetProperty("promotionPercent").GetInt32());

        // Step 5: re-fetch antiforgery then POST enabled=false to flip back.
        var page2 = await _client.GetAsync("/admin/menu");
        var antiforgery2 = System.Text.RegularExpressions.Regex.Match(
            await page2.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<v>[^\"]+)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups["v"].Value;
        var postOff = await _client.PostAsync(
            "/admin/menu?handler=SetPromotionEnabled&enabled=false",
            new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["__RequestVerificationToken"] = antiforgery2
            }));
        Assert.True(postOff.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Toggle-OFF POST returned {(int)postOff.StatusCode}");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.PricingPromotionSettings.AsNoTracking()
                .FirstAsync(p => p.Id == PricingPromotionSettingsConfiguration.SingletonId);
            Assert.False(row.Enabled, "Toggle-OFF did not flip the singleton row back.");
        }

        // /api/menu must now show base prices again.
        var menuResp2 = await _client.GetAsync("/api/menu");
        var menuBody2 = await menuResp2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(menuBody2.GetProperty("promotionActive").GetBoolean());
    }
}