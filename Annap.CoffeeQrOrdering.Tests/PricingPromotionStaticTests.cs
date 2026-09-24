using System.Reflection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Source-level invariants for the global "Giảm giá 10%" promotion feature.
///
/// These tests guard the architecture contract documented in the audit:
///   • MenuItem.Price is the base price and is NEVER mutated by the toggle.
///   • All effective-price paths route through IPricingService — no
///     duplicate <c>price * 0.9</c> / <c>price * 0.9m</c> scattered in code.
///   • The Admin toggle is server-authoritative and Admin-only via the
///     page-level <c>[Authorize(Policy = "Staff")]</c>.
///   • The promotion toggle is OFF by default at deploy time.
///   • Historical OrderItem.UnitPrice is the snapshot the rest of the
///     pipeline reads from; nothing recalculates it from current settings.
/// </summary>
public sealed class PricingPromotionStaticTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Repository paths (matches sibling *StaticTests files).
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web"));

    private static readonly string DomainRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Domain"));

    private static readonly string InfrastructureRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Infrastructure"));

    private static readonly string ApplicationRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Application"));

    private static readonly string PagesRoot = Path.Combine(WebRoot, "Pages");
    private static readonly string WebSrcRoot = Path.Combine(WebRoot);

    private static readonly string IndexPageModelPath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml.cs");

    private static readonly string IndexPagePath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml");

    private static readonly string DrinkCardPartialPath = Path.Combine(
        PagesRoot, "Shared", "_MenuEditorialDrinkCard.cshtml");

    private static readonly string GuestMenuPagePath = Path.Combine(
        PagesRoot, "Menu", "Index.cshtml");

    private static readonly string GuestMenuPageModelPath = Path.Combine(
        PagesRoot, "Menu", "Index.cshtml.cs");

    private static readonly string EndpointsPath = Path.Combine(
        WebSrcRoot, "Extensions", "EndpointExtensions.cs");

    private static readonly string PricingServicePath = Path.Combine(
        InfrastructureRoot, "Services", "PricingService.cs");

    private static readonly string PromotionSettingsPath = Path.Combine(
        DomainRoot, "Entities", "PricingPromotionSettings.cs");

    private static readonly string PromotionSettingsConfigurationPath = Path.Combine(
        InfrastructureRoot, "Persistence", "Configurations", "PricingPromotionSettingsConfiguration.cs");

    private static readonly string PromotionSettingsMigrationPath = Path.Combine(
        InfrastructureRoot, "Persistence", "Migrations", "20260924165814_AddPricingPromotionSettings.cs");

    private static readonly string AppDbContextPath = Path.Combine(
        InfrastructureRoot, "Persistence", "AppDbContext.cs");

    private static readonly string InfrastructureDiPath = Path.Combine(
        InfrastructureRoot, "DependencyInjection.cs");

    private static readonly string IApplicationDbContextPath = Path.Combine(
        ApplicationRoot, "Abstractions", "IApplicationDbContext.cs");

    private static readonly string MenuItemPath = Path.Combine(
        DomainRoot, "Entities", "MenuItem.cs");

    private static readonly string OrderItemPath = Path.Combine(
        DomainRoot, "Entities", "OrderItem.cs");

    private static string ReadFile(string path) =>
        File.ReadAllText(Path.GetFullPath(path));

    // ─────────────────────────────────────────────────────────────────────────
    // 1) Domain entity: default OFF.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingPromotionSettings_entity_defaults_to_off_in_source()
    {
        var src = ReadFile(PromotionSettingsPath);
        Assert.Contains("public bool Enabled { get; set; }", src, StringComparison.Ordinal);
        // The whole-number Percent is documented as 10 (the "Giảm giá 10%" UX).
        Assert.Contains("public int Percent { get; set; } = 10;", src, StringComparison.Ordinal);
        // The base row MUST start disabled — a fresh deploy must never auto-apply.
        Assert.DoesNotContain("public bool Enabled { get; set; } = true;", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2) Configuration: fixed SingletonId for the global settings row.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingPromotionSettings_configuration_exposes_singleton_id()
    {
        var src = ReadFile(PromotionSettingsConfigurationPath);
        Assert.Contains("public static readonly Guid SingletonId", src, StringComparison.Ordinal);
        // The seeded id must be stable so the migration INSERT and runtime
        // LoadSettingsAsync agree. Hard-coded so a deploy never creates a
        // second row that would drift from the singleton contract.
        Assert.Contains("cccccccc-cccc-cccc-cccc-ccccccccccc1", src, StringComparison.Ordinal);
        // Configuration sets Enabled default to false at the EF level so a
        // new row (defensive re-seed) can never start ON. Multi-line OK.
        Assert.Contains("builder.Property(x => x.Enabled)", src, StringComparison.Ordinal);
        Assert.Contains(".HasDefaultValue(false);", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3) Migration: seeds the singleton in OFF state and is reversible.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Promotion_migration_seeds_off_singleton_and_is_reversible()
    {
        var src = ReadFile(PromotionSettingsMigrationPath);
        Assert.Contains("migrationBuilder.CreateTable", src, StringComparison.Ordinal);
        Assert.Contains("name: \"pricing_promotion_settings\"", src, StringComparison.Ordinal);
        Assert.Contains("migrationBuilder.InsertData(", src, StringComparison.Ordinal);
        // The seeded row must be Enabled = false. (Multi-line, so we look for
        // the row contents separately.)
        Assert.Contains("new Guid(\"cccccccc-cccc-cccc-cccc-ccccccccccc1\")", src, StringComparison.Ordinal);
        Assert.Contains(" false, 10,", src, StringComparison.Ordinal);
        // The down() migration drops the table so the feature is fully reversible.
        Assert.Contains("migrationBuilder.DropTable(", src, StringComparison.Ordinal);
        Assert.Contains("name: \"pricing_promotion_settings\"", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4) DbContext: DbSet registered on both interface and concrete context.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingPromotionSettings_is_registered_in_application_db_context()
    {
        var iface = ReadFile(IApplicationDbContextPath);
        Assert.Contains("DbSet<PricingPromotionSettings> PricingPromotionSettings { get; }", iface, StringComparison.Ordinal);

        var concrete = ReadFile(AppDbContextPath);
        Assert.Contains("public DbSet<PricingPromotionSettings> PricingPromotionSettings => Set<PricingPromotionSettings>();", concrete, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5) DI: PricingService is registered scoped.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingService_is_registered_in_infrastructure_di()
    {
        var src = ReadFile(InfrastructureDiPath);
        Assert.Contains("services.AddScoped<IPricingService, PricingService>();", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6) Single arithmetic path: rounding policy lives in one place.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingService_uses_away_from_zero_rounding_and_single_arithmetic_path()
    {
        var src = ReadFile(PricingServicePath);
        Assert.Contains("MidpointRounding.AwayFromZero", src, StringComparison.Ordinal);
        // The exact arithmetic must be a single (100 - Percent) / 100 path so
        // future readers can't accidentally double-discount by multiplying
        // again elsewhere.
        Assert.Contains("var multiplier = (100m - percent) / 100m;", src, StringComparison.Ordinal);
        Assert.Contains("return Math.Round(raw, MidpointRounding.AwayFromZero);", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7) MenuItem.Price is the immutable base price.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void MenuItem_entity_has_only_base_price_no_sale_price_field()
    {
        var src = ReadFile(MenuItemPath);
        Assert.Contains("public decimal Price { get; set; }", src, StringComparison.Ordinal);
        // No second price field. Sale/discount lives on the global promotion
        // singleton; the entity never carries a second price column that
        // could be out of sync with the toggle.
        Assert.DoesNotContain("OriginalPrice", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SalePrice", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DiscountedPrice", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8) OrderItem: UnitPrice is a snapshot (no setter that recomputes).
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void OrderItem_UnitPrice_is_a_writable_snapshot_field()
    {
        var src = ReadFile(OrderItemPath);
        Assert.Contains("public decimal UnitPrice { get; set; }", src, StringComparison.Ordinal);
        // No recalculation method on the entity: the price is set once at
        // order submit and never re-derived from the global promotion.
        Assert.DoesNotContain("RecalculateUnitPrice", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RecomputePrice", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9) Admin page: reads promotion state on GET and exposes toggle handler.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Admin_menu_index_page_model_reads_promotion_state_and_exposes_toggle()
    {
        var src = ReadFile(IndexPageModelPath);

        // GET path reads state via IPricingService.
        Assert.Contains("PromotionEnabled = await pricing.IsEnabledAsync(cancellationToken);", src, StringComparison.Ordinal);
        Assert.Contains("PromotionPercent = await pricing.GetCurrentPercentAsync(cancellationToken);", src, StringComparison.Ordinal);

        // The page injects IPricingService via the constructor (primary ctor).
        Assert.Contains("IPricingService pricing", src, StringComparison.Ordinal);

        // POST handler exists and updates the singleton row.
        Assert.Contains("OnPostSetPromotionEnabledAsync", src, StringComparison.Ordinal);
        Assert.Contains("PricingPromotionSettingsConfiguration.SingletonId", src, StringComparison.Ordinal);
        Assert.Contains("db.PricingPromotionSettings", src, StringComparison.Ordinal);

        // Toggle handler must NEVER mutate MenuItem.Price directly — it must
        // only flip the Enabled flag so toggling OFF automatically restores
        // the original prices.
        var handlerStart = src.IndexOf("OnPostSetPromotionEnabledAsync", StringComparison.Ordinal);
        var handlerEnd = src.IndexOf("/// <summary>Canonical status values", StringComparison.Ordinal);
        Assert.True(handlerStart > 0, "OnPostSetPromotionEnabledAsync must exist.");
        Assert.True(handlerEnd > handlerStart, "Boundary for the toggle handler body must follow.");
        var handlerBody = src[handlerStart..handlerEnd];
        Assert.DoesNotContain("MenuItems", handlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("item.Price =", handlerBody, StringComparison.Ordinal);
        Assert.DoesNotContain(".Price = ", handlerBody, StringComparison.Ordinal);

        // Audit row emitted so the toggle is observable in OperationalAuditEntries.
        Assert.Contains("OperationalAudit.AppendAsync", handlerBody, StringComparison.Ordinal);
        Assert.Contains("\"menu.promotion_toggle\"", handlerBody, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10) Admin page: Admin-only authorization is preserved.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Admin_menu_page_remains_admin_only_via_policy()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("[Authorize(Policy = \"Staff\")]", src, StringComparison.Ordinal);
        // The "Staff" policy is admin-only (see DependencyInjectionExtensions).
        // We do NOT introduce a new policy: the existing Admin Menu CRUD
        // already requires admin, so the toggle inherits that.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11) Admin page razor: toggle UI is rendered, with explicit current state.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Admin_menu_page_razor_renders_toggle_with_current_state_label()
    {
        var src = ReadFile(IndexPagePath);
        // The form posts to the SetPromotionEnabled handler.
        Assert.Contains("asp-page-handler=\"SetPromotionEnabled\"", src, StringComparison.Ordinal);
        // Two explicit state labels so the operator can read the current
        // mode at a glance — one for ON, one for OFF.
        Assert.Contains("ĐANG BẬT", src, StringComparison.Ordinal);
        Assert.Contains("TẮT", src, StringComparison.Ordinal);
        // The button text reflects the next action, never just "Submit".
        Assert.Contains("Bật giảm giá", src, StringComparison.Ordinal);
        Assert.Contains("Tắt giảm giá", src, StringComparison.Ordinal);
        // Antiforgery token is required.
        Assert.Contains("@Html.AntiForgeryToken()", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12b) Guest card partial: shows effective price + strike-through original.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Drink_card_partial_uses_effective_price_and_optional_original()
    {
        var src = ReadFile(DrinkCardPartialPath);
        // New model fields appended — backwards-incompatible signature change
        // is intentional and limited to this partial's two call sites.
        Assert.Contains("decimal EffectivePrice", src, StringComparison.Ordinal);
        Assert.Contains("decimal? OriginalPrice", src, StringComparison.Ordinal);
        // Strike-through is rendered conditionally.
        Assert.Contains("menu-editorial-card__price-strike", src, StringComparison.Ordinal);
        // Effective price is the price the guest pays.
        Assert.Contains("var effectiveDisplay = effectivePrice > 0", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12c) CSS: the strike-through classes must have visual styling so the
    //        original price isn't rendered as a duplicate of the effective price.
    //        This catches regressions where the markup is added but the CSS is
    //        forgotten — the exact bug observed when the promotion was first
    //        enabled in production.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Price_strike_css_classes_are_defined_in_wwwroot()
    {
        var menuBrowseCssPath = Path.Combine(WebSrcRoot, "wwwroot", "css", "menu-browse.css");
        var drinkDetailCssPath = Path.Combine(WebSrcRoot, "wwwroot", "css", "drink-detail.css");

        var menuCss = File.ReadAllText(menuBrowseCssPath);
        var detailCss = File.ReadAllText(drinkDetailCssPath);

        // Menu editorial card strike-through.
        Assert.Contains("menu-editorial-card__price-strike", menuCss, StringComparison.Ordinal);
        Assert.Contains("text-decoration: line-through", menuCss, StringComparison.Ordinal);
        // Menu editorial card price block wrapper.
        Assert.Contains("menu-editorial-card__price-block", menuCss, StringComparison.Ordinal);

        // Detail card strike-through.
        Assert.Contains("dd-card__price-strike", detailCss, StringComparison.Ordinal);
        Assert.Contains("text-decoration: line-through", detailCss, StringComparison.Ordinal);
        // Detail card price block wrapper.
        Assert.Contains("dd-card__price-block", detailCss, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12d) Pricing invariant: every detail renderer emits the effective price
    //        via `data-item-price` so the cart and the receipt see the same
    //        number, not the MenuItem.Price base.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Detail_renderer_writes_effective_price_into_data_item_price()
    {
        var js = File.ReadAllText(Path.Combine(WebSrcRoot, "wwwroot", "js", "drink-detail-renderer.js"));
        // The in-card CTA must carry the effective price (raw.price normalized).
        Assert.Contains("data-item-price=\"' + escAttr(data.price", js, StringComparison.Ordinal);
        // The pairing cards must also carry the effective price.
        Assert.Contains("data-item-price=\"' + escAttr(p.price", js, StringComparison.Ordinal);
        // The original price is read separately for the strike-through.
        Assert.Contains("data.originalPrice", js, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12e) Detail modal `syncTopCta` feeds the effective price into the top
    //         "Pha cho bàn tôi" CTA dataset so `annapAddToCart` reads the same
    //         number as the visible price.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Detail_modal_syncTopCta_writes_effective_price_into_top_cta_dataset()
    {
        var js = File.ReadAllText(Path.Combine(WebSrcRoot, "wwwroot", "js", "drink-detail-modal.js"));
        // Locate the syncTopCta function body.
        var start = js.IndexOf("function syncTopCta", StringComparison.Ordinal);
        Assert.True(start > 0, "syncTopCta missing");
        var end = js.IndexOf("\n        function ", start + 1, StringComparison.Ordinal);
        var block = end > start ? js[start..end] : js[start..];
        // Must read from the normalized `d.price` (effective).
        Assert.Contains("d.price", block, StringComparison.Ordinal);
        Assert.Contains("data-item-price", block, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13) Guest menu page: catalog JSON carries effective price fields.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guest_menu_page_exposes_effective_price_in_catalog_json()
    {
        var src = ReadFile(GuestMenuPagePath);
        // Catalog rows ship the effective price as `price` (single number the
        // cart uses) and the base as `originalPrice` for strike-through.
        Assert.Contains("price = effectivePrices.TryGetValue(i.Id, out var ep) ? ep : i.Price", src, StringComparison.Ordinal);
        Assert.Contains("originalPrice = originalPrices.TryGetValue(i.Id, out var op) ? (decimal?)op : null", src, StringComparison.Ordinal);
        Assert.Contains("promotionActive", src, StringComparison.Ordinal);
        Assert.Contains("promotionPercent", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14) Guest menu page model: calls IPricingService for every visible item.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guest_menu_page_model_calls_pricing_service_for_every_item()
    {
        var src = ReadFile(GuestMenuPageModelPath);
        // Promotion state resolved once at the top of the handler.
        Assert.Contains("pricing.GetCurrentPercentAsync", src, StringComparison.Ordinal);
        Assert.Contains("PromotionActive = PromotionPercent > 0", src, StringComparison.Ordinal);
        // Per-item effective price must be computed inside the same loop
        // that materializes the cards.
        Assert.Contains("pricing.GetEffectivePriceAsync(item.Price, cancellationToken)", src, StringComparison.Ordinal);
        // originalByItem only populated when promotion actually changed the
        // price — guarantees strike-through is not shown when OFF.
        Assert.Contains("if (PromotionActive && effective != item.Price)", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 15) /api/menu: server-authoritative effective price + flag.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Menu_endpoint_emits_effective_price_and_promotion_flag()
    {
        var src = ReadFile(EndpointsPath);
        Assert.Contains("await pricing.IsEnabledAsync(ct);", src, StringComparison.Ordinal);
        Assert.Contains("await pricing.GetCurrentPercentAsync(ct);", src, StringComparison.Ordinal);
        // Per-item effective price is computed via the same service.
        Assert.Contains("await pricing.GetEffectivePriceAsync(i.Price, ct);", src, StringComparison.Ordinal);
        // The DTO carries both base price (for transparency) and the
        // EffectivePrice the guest will pay.
        Assert.Contains("i.Price,", src, StringComparison.Ordinal);
        Assert.Contains("EffectivePrice = effective,", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 16) POST /api/orders: snapshots effective price; no client-side price field.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Order_submit_endpoint_snapshots_effective_unit_price_from_server()
    {
        var src = ReadFile(EndpointsPath);
        // The handler resolves every unit price via IPricingService.
        Assert.Contains("effectivePrices[mi.Id] = await pricing.GetEffectivePriceAsync(mi.Price, ct);", src, StringComparison.Ordinal);
        // Snapshot on OrderItem.UnitPrice.
        Assert.Contains("UnitPrice = effectivePrices[i.MenuItemId],", src, StringComparison.Ordinal);
        // The historical-orders guarantee: the snapshot is the source of
        // truth from this point forward. No code path recalculates later.
        Assert.Contains("Historical orders are NEVER recalculated", src, StringComparison.Ordinal);
        // No `unitPrice` property appears in the CreateOrderRequest body so
        // a malicious client cannot ship a price. We only see MenuItemId +
        // Quantity + Notes + CustomerNote + Temperature.
        // Bound by JSON: assert via the request DTO literal.
        Assert.DoesNotContain("unitPrice =", src, StringComparison.Ordinal);
        Assert.DoesNotContain("UnitPrice = request.", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 17) No scattered "price * 0.9" — discount lives in PricingService only.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void No_scattered_price_multiplication_in_pages_or_endpoints()
    {
        // Search every .cshtml.cs under Pages/Admin and Pages/Menu and the
        // endpoint files for any discount-style multiplication. The
        // contract is single arithmetic path; finding any other place that
        // applies a discount would mean a refactor slipped through.
        var disallowedSources = new[]
        {
            IndexPageModelPath,
            GuestMenuPageModelPath,
            EndpointsPath,
        };

        foreach (var path in disallowedSources)
        {
            var src = ReadFile(path);
            Assert.DoesNotContain("* 0.9", src, StringComparison.Ordinal);
            Assert.DoesNotContain("* 0.9m", src, StringComparison.Ordinal);
            Assert.DoesNotContain("* (decimal)0.9", src, StringComparison.Ordinal);
            Assert.DoesNotContain("/= 10m * 9m", src, StringComparison.Ordinal);
        }

        // Sanity: the partial also does NOT apply its own discount — it only
        // renders numbers provided by the page model.
        var partial = ReadFile(DrinkCardPartialPath);
        Assert.DoesNotContain("* 0.9", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("* 0.9m", partial, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 18) Discount clamp: percent never exceeds 100, never goes negative.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void PricingService_clamps_percent_to_zero_and_hundred()
    {
        var src = ReadFile(PricingServicePath);
        Assert.Contains("if (pct < 0) return 0;", src, StringComparison.Ordinal);
        Assert.Contains("if (pct > 100) return 100;", src, StringComparison.Ordinal);
        // Same clamps on the effective-price path.
        Assert.Contains("if (percent > 100) percent = 100;", src, StringComparison.Ordinal);
    }
}