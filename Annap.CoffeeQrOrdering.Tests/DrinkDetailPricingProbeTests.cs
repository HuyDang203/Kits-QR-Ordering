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
/// End-to-end coverage for the Guest-facing pricing surfaces:
/// detail Data handler, in-card &amp; top CTA, catalog JSON, /api/menu,
/// and POST /api/orders. These tests guarantee the same effective price
/// appears on every surface so a guest can never see a different number
/// from the one the server will snapshot on OrderItem.UnitPrice.
/// </summary>
public sealed class DrinkDetailPricingProbeTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private async Task ResetPromotionAsync(bool enabled)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sid = PricingPromotionSettingsConfiguration.SingletonId;
        var row = await db.PricingPromotionSettings.FirstOrDefaultAsync(p => p.Id == sid);
        if (row is null)
        {
            db.PricingPromotionSettings.Add(new PricingPromotionSettings { Id = sid, Enabled = enabled, Percent = 10 });
        }
        else
        {
            row.Enabled = enabled;
            row.Percent = 10;
        }
        await db.SaveChangesAsync();
    }

    private async Task<MenuItem> SeedAsync(decimal price, string name, string categoryName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = await db.MenuCategories.FirstOrDefaultAsync(c => c.Name == categoryName);
        if (cat is null)
        {
            cat = new MenuCategory { Name = categoryName, SortOrder = 999 };
            db.MenuCategories.Add(cat);
            await db.SaveChangesAsync();
        }
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

    private async Task<VenueTable> SeedTableAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = $"T{suffix[..2]}{suffix[2..4]}",
            PublicSlug = $"annap-probe-{suffix}",
            DisplayLabel = $"Probe {suffix[..4]}",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync();
        return table;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1) Drink Detail Data handler returns BOTH prices — the JS renderer needs
    //    `originalPrice` for the strike-through and `price` for the effective
    //    number shown to the guest. When promotion is OFF they must match.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Detail_Data_returns_effective_price_with_original_when_promotion_on()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedAsync(25_000m, "Probe Black Coffee", "Probe Category ON");
        await ResetPromotionAsync(enabled: true);

        var resp = await _client.GetAsync($"/menu/drink/{item.Id:D}?handler=Data");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, $"Status {(int)resp.StatusCode}: {body}");

        var json = JsonDocument.Parse(body);
        var price = json.RootElement.GetProperty("price").GetDecimal();
        var originalPrice = json.RootElement.GetProperty("originalPrice").GetDecimal();
        var priceDisplay = json.RootElement.GetProperty("priceDisplay").GetString();
        var originalPriceDisplay = json.RootElement.GetProperty("originalPriceDisplay").GetString();

        Assert.Equal(25_000m, originalPrice);
        Assert.Equal(22_500m, price);
        Assert.Equal("22.500 ₫", priceDisplay);
        Assert.Equal("25.000 ₫", originalPriceDisplay);
    }

    [Fact]
    public async Task Detail_Data_returns_base_price_for_both_when_promotion_off()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedAsync(25_000m, "Probe Black Coffee OFF", "Probe Category OFF");

        var resp = await _client.GetAsync($"/menu/drink/{item.Id:D}?handler=Data");
        Assert.True(resp.IsSuccessStatusCode);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        var price = json.RootElement.GetProperty("price").GetDecimal();
        var originalPrice = json.RootElement.GetProperty("originalPrice").GetDecimal();
        var priceDisplay = json.RootElement.GetProperty("priceDisplay").GetString();

        // OFF: effective == base; renderer should NOT show a strike-through.
        Assert.Equal(25_000m, originalPrice);
        Assert.Equal(25_000m, price);
        Assert.Equal("25.000 ₫", priceDisplay);

        // originalPriceDisplay is nullable; must be null/absent when promotion
        // is off so the renderer can skip the strike-through span.
        if (json.RootElement.TryGetProperty("originalPriceDisplay", out var opd) &&
            opd.ValueKind == JsonValueKind.String)
        {
            Assert.Fail($"originalPriceDisplay must be null/empty when promotion is OFF, got '{opd.GetString()}'");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2) Guest menu page renders BOTH prices in the inlined catalog JSON. The
    //    JS catalog cache (`menu-catalog-json`) is the fallback when an
    //    add-to-cart source button has no `data-item-price` attribute, so the
    //    `price` field must already be the effective price.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Guest_menu_page_catalog_json_carries_effective_price_per_item()
    {
        await ResetPromotionAsync(enabled: false);
        var itemA = await SeedAsync(25_000m, "Catalog A", "Catalog Cat A");
        var itemB = await SeedAsync(40_000m, "Catalog B", "Catalog Cat B");
        await ResetPromotionAsync(enabled: true);

        // The menu page requires a seated table to render the catalog body.
        // We don't need the table id; we just need to scrape the inlined JSON.
        var page = await _client.GetAsync("/menu");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();

        var startIdx = html.IndexOf("menu-catalog-json", StringComparison.Ordinal);
        Assert.True(startIdx > 0, "menu-catalog-json script not found");
        var jsonStart = html.IndexOf('>', startIdx) + 1;
        var jsonEnd = html.IndexOf("</script>", jsonStart, StringComparison.Ordinal);
        Assert.True(jsonEnd > jsonStart);
        var rawJson = html.Substring(jsonStart, jsonEnd - jsonStart);

        var catalog = JsonDocument.Parse(rawJson).RootElement;
        var ids = new HashSet<Guid> { itemA.Id, itemB.Id };

        var foundA = false;
        var foundB = false;
        foreach (var row in catalog.EnumerateArray())
        {
            var id = row.GetProperty("id").GetGuid();
            if (!ids.Contains(id)) continue;
            var price = row.GetProperty("price").GetDecimal();
            var original = row.TryGetProperty("originalPrice", out var op) && op.ValueKind == JsonValueKind.Number
                ? op.GetDecimal()
                : (decimal?)null;
            if (id == itemA.Id)
            {
                foundA = true;
                Assert.Equal(22_500m, price);
                Assert.NotNull(original);
                Assert.Equal(25_000m, original!.Value);
            }
            else if (id == itemB.Id)
            {
                foundB = true;
                Assert.Equal(36_000m, price);
                Assert.NotNull(original);
                Assert.Equal(40_000m, original!.Value);
            }
        }
        Assert.True(foundA, "Item A not in catalog JSON");
        Assert.True(foundB, "Item B not in catalog JSON");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3) POST /api/orders snapshot = effective price from PricingService.
    //    This guards the cart→checkout→receipt invariant when the client
    //    sends a forged unitPrice (it MUST be ignored).
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(true, 25_000, 22_500)]   // ON
    [InlineData(false, 25_000, 25_000)]  // OFF
    public async Task Order_submit_snapshots_effective_unit_price_for_drink_detail(
        bool promotionOn, int basePrice, int expectedUnitPrice)
    {
        await ResetPromotionAsync(enabled: promotionOn);
        var table = await SeedTableAsync();
        var item = await SeedAsync(basePrice, $"Snapshot {(promotionOn ? "ON" : "OFF")}", "Snapshot Cat");

        // Forge a client unitPrice to confirm the server ignores it.
        var payload = new
        {
            venueTableId = table.Id,
            fulfillmentMode = "DineIn",
            paymentMethod = "Cash",
            idempotencyKey = Guid.NewGuid().ToString("N"),
            items = new[]
            {
                new
                {
                    menuItemId = item.Id,
                    quantity = 2,
                    unitPrice = 1m  // malicious; must be ignored
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
        Assert.Equal(expectedUnitPrice, (int)line.UnitPrice);

        var expectedTotal = expectedUnitPrice * 2;
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
        Assert.Equal(expectedTotal, (int)order.TotalAmount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4) No double discount across two decimal items. 25_000 → 22_500 ON
    //    must NEVER become 20_250 even after the cart round-trip.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Promotion_on_never_double_discounts_for_drink_detail_add()
    {
        await ResetPromotionAsync(enabled: true);
        var table = await SeedTableAsync();
        var item = await SeedAsync(25_000m, "Double Discount Probe", "DDD Cat");

        var resp = await _client.PostAsJsonAsync("/api/orders", new
        {
            venueTableId = table.Id,
            fulfillmentMode = "DineIn",
            paymentMethod = "Cash",
            idempotencyKey = Guid.NewGuid().ToString("N"),
            items = new[] { new { menuItemId = item.Id, quantity = 1 } }
        }, JsonOpts);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        var total = body.GetProperty("totalAmount").GetDecimal();
        // The contract is 22_500 once and only once. 20_250 would mean a
        // re-application of the 10 % multiplier at submit time.
        Assert.Equal(22_500m, total);
        Assert.NotEqual(20_250m, total);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5) Guest menu page renders BOTH prices in the editorial card partial —
    //    the server-side Razor output is the visible price the guest sees
    //    before any JS executes. The strike-through must be present and the
    //    effective price must be the headline.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Guest_menu_page_card_partial_renders_effective_price_and_strike()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedAsync(25_000m, "Card Render Probe", "Card Render Cat");
        var table = await SeedTableAsync();
        await ResetPromotionAsync(enabled: true);

        // Seated URL so the Razor body actually renders the catalog cards.
        var page = await _client.GetAsync($"/menu?vt={table.Id:D}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();

        // The effective price must appear in the rendered card.
        // (HTML encodes ₫ as &#x20AB; — compare the numeric prefix which
        //  is stable across both encodings.)
        Assert.Contains("22.500", html, StringComparison.Ordinal);
        // The original price must appear in the strike-through span when ON.
        Assert.Contains("25.000", html, StringComparison.Ordinal);
        // The strike-through CSS class must be applied.
        Assert.Contains("menu-editorial-card__price-strike", html, StringComparison.Ordinal);
        // The price-block CSS class must wrap both.
        Assert.Contains("menu-editorial-card__price-block", html, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6) Promotion OFF: the menu editorial card must NOT show a strike-through
    //    — otherwise the guest would see the original price as a duplicate.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Guest_menu_page_card_partial_omits_strike_when_promotion_off()
    {
        await ResetPromotionAsync(enabled: false);
        var item = await SeedAsync(25_000m, "Card Render Probe OFF", "Card Render Cat OFF");
        var table = await SeedTableAsync();

        var page = await _client.GetAsync($"/menu?vt={table.Id:D}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();

        Assert.Contains("25.000", html, StringComparison.Ordinal);
        // No strike-through CSS class should appear anywhere when OFF.
        Assert.DoesNotContain("menu-editorial-card__price-strike", html, StringComparison.Ordinal);
    }
}
