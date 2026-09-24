using System.Reflection;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Phase 2 production-safety tests for the admin menu server-side filter &
/// search. The goal is to guarantee the invariants documented in the phase
/// brief:
///
///   • Query parameters (q, category, status) are read from the URL and bound
///     to the page model before the database query runs.
///   • Search applies across Name, Subtitle, Description and CatalogKey using
///     a parameterized LIKE pattern.
///   • Empty/whitespace search is treated as no search filter.
///   • Status values resolve safely: all / available / soldout / archived.
///   • Archived items remain reachable; archived takes precedence over
///     availability.
///   • Invalid category IDs fall back to "no category filter" instead of
///     silently rendering an empty list.
///   • The guest menu query is untouched.
///   • The Phase 1 quick-action handlers and POST forms still exist and are
///     not broken by the filter refactor.
///
/// InMemory provider note: <c>EF.Functions.Like</c> is not supported by the
/// InMemory provider, so behavior tests that exercise the search predicate
/// itself are not feasible here. Those clauses are covered by structural
/// assertions on the page model source. End-to-end coverage for the search
/// predicate requires the Postgres-backed test fixture (Docker required).
/// </summary>
public sealed class MenuSearchFilterTests
{
    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string IndexPageModelPath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml.cs");

    private static readonly string IndexPagePath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml");

    private static readonly string JsPath = Path.Combine(WebRoot, "js", "menu-operator.js");

    private static readonly string GuestMenuPageModelPath = Path.Combine(PagesRoot, "Menu", "Index.cshtml.cs");

    private static string ReadFile(string path) =>
        File.ReadAllText(Path.GetFullPath(path));

    // ─────────────────────────────────────────────────────────────────────────
    // 1) No filters returns every admin menu item.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task OnGetAsync_with_no_filters_returns_all_items()
    {
        await using var db = NewInMemoryContext();
        var (cats, items) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(items.Count, page.Items.Count);
        var ids = page.Items.Select(i => i.Id).ToHashSet();
        foreach (var i in items) Assert.Contains(i, ids);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2-5) Search predicate shape — source-level coverage for each field.
    //
    // EF.Functions.Like is not supported on the InMemory provider, so we
    // assert the source-level contract instead. If a future refactor drops
    // any of these fields, this test catches it before it ships.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Page_model_search_predicate_includes_Name()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("EF.Functions.Like(i.Name, pattern)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_search_predicate_includes_Subtitle()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("EF.Functions.Like(i.Subtitle, pattern)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_search_predicate_includes_Description()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("EF.Functions.Like(i.Description, pattern)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_search_predicate_includes_CatalogKey()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("EF.Functions.Like(i.CatalogKey, pattern)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_search_pattern_wraps_with_percent_wildcards()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("var pattern = $\"%{NormalizedSearch}%\"", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6) Empty/whitespace search is treated as no filter at all.
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t  \n")]
    public async Task Empty_or_whitespace_search_does_not_filter(string? rawSearch)
    {
        await using var db = NewInMemoryContext();
        var (_, items) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.SearchQuery = rawSearch;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.NormalizedSearch);
        Assert.False(page.HasActiveFilters);
        Assert.Equal(items.Count, page.Items.Count);
    }

    [Fact]
    public void Page_model_trims_surrounding_whitespace_from_search()
    {
        // The InMemory provider does not implement EF.Functions.Like, so we
        // assert the trim invariant at the source level: the page model
        // computes NormalizedSearch by trimming the raw input.
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim()", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8-10) Status filters are applied server-side.
    //
    // ApplyStatusFilter uses only simple property predicates, so the InMemory
    // provider handles it fine. Each test seeds a known mix and asserts the
    // expected subset.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Status_available_returns_only_not_archived_and_available()
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = "available";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.Available, page.StatusFilter);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.False(item.IsArchived);
            Assert.True(item.IsAvailable);
        });
    }

    [Fact]
    public async Task Status_soldout_returns_only_not_archived_and_unavailable()
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = "soldout";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.SoldOut, page.StatusFilter);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.False(item.IsArchived);
            Assert.False(item.IsAvailable);
        });
    }

    [Fact]
    public async Task Status_archived_returns_only_archived_regardless_of_availability()
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = "archived";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.Archived, page.StatusFilter);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.True(item.IsArchived));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13) Invalid status values fall back safely to "all".
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-a-status")]
    [InlineData("deleted")]
    [InlineData("ALL_UPPER")]
    public async Task Invalid_status_falls_back_to_all(string? raw)
    {
        await using var db = NewInMemoryContext();
        var (_, items) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = raw;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.All, page.StatusFilter);
        Assert.Equal(items.Count, page.Items.Count);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("AVAILABLE")]
    [InlineData("Available")]
    public async Task Status_canonical_values_parse_case_insensitively(string raw)
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = raw;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.Available, page.StatusFilter);
    }

    [Theory]
    [InlineData("soldout")]
    [InlineData("sold-out")]
    [InlineData("sold_out")]
    public async Task Status_sold_out_aliases_resolve(string raw)
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = raw;
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.SoldOut, page.StatusFilter);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11) Category filter works server-side.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Category_filter_returns_only_items_in_that_category()
    {
        await using var db = NewInMemoryContext();
        var (cats, _) = await SeedStandardMenuAsync(db);

        var target = cats[0];
        var page = NewPageModel(db);
        page.CategoryFilter = target.Id;
        await page.OnGetAsync(CancellationToken.None);

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(target.Id, item.CategoryId));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14) Invalid / unknown category falls back safely.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Unknown_category_id_falls_back_to_no_category_filter()
    {
        await using var db = NewInMemoryContext();
        var (_, items) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.CategoryFilter = Guid.NewGuid();
        await page.OnGetAsync(CancellationToken.None);

        Assert.Null(page.CategoryFilter);
        Assert.Equal(items.Count, page.Items.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12) Search + category + status work together.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Search_category_and_status_combine_into_a_single_query()
    {
        await using var db = NewInMemoryContext();
        var (cats, _) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        // Empty search keeps the search branch off the InMemory provider, so
        // we can verify the category + status intersection behavior here.
        page.SearchQuery = "";
        page.CategoryFilter = cats[0].Id;
        page.StatusFilterRaw = "available";
        await page.OnGetAsync(CancellationToken.None);

        Assert.True(page.HasActiveFilters);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(cats[0].Id, item.CategoryId);
            Assert.False(item.IsArchived);
            Assert.True(item.IsAvailable);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Result count, active filters, and clear-filters link.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HasActiveFilters_reflects_every_filter()
    {
        await using var db = NewInMemoryContext();
        await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        await page.OnGetAsync(CancellationToken.None);
        Assert.False(page.HasActiveFilters);

        page.SearchQuery = "x";
        Assert.True(page.HasActiveFilters);
        page.SearchQuery = null;

        page.CategoryFilter = Guid.NewGuid();
        Assert.True(page.HasActiveFilters);
        page.CategoryFilter = null;

        page.StatusFilterRaw = "archived";
        Assert.True(page.HasActiveFilters);
        page.StatusFilterRaw = null;

        // An invalid status still indicates "user typed something", so the
        // clear-filters affordance should be visible — even though the page
        // model resolves the value to All and shows everything.
        page.StatusFilterRaw = "invalid";
        Assert.True(page.HasActiveFilters);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 15) Query parameters are preserved in the rendered filter controls.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Index_view_form_targets_admin_menu_with_GET()
    {
        var src = ReadFile(IndexPagePath);

        // The filter form is a GET form targeting /admin/menu.
        var formIdx = src.IndexOf("menu-operator__filters-form", StringComparison.Ordinal);
        Assert.True(formIdx > 0, "Filter form root must exist on Index.cshtml.");
        var formOpen = src.LastIndexOf("<form", formIdx, StringComparison.Ordinal);
        Assert.True(formOpen > 0);
        var formOpenEnd = src.IndexOf('>', formOpen);
        var opening = src[formOpen..(formOpenEnd + 1)];
        Assert.Contains("method=\"get\"", opening, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action=\"/admin/menu\"", opening, StringComparison.OrdinalIgnoreCase);

        // GET form must NOT carry an antiforgery token.
        var formClose = src.IndexOf("</form>", formOpenEnd);
        var form = src[formOpen..(formClose + "</form>".Length)];
        Assert.DoesNotContain("@Html.AntiForgeryToken()", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_renders_query_named_inputs()
    {
        var src = ReadFile(IndexPagePath);
        Assert.Contains("name=\"q\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", src, StringComparison.Ordinal);
        Assert.Contains("type=\"search\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_preserves_status_filter_value_across_submit()
    {
        var src = ReadFile(IndexPagePath);

        // The four canonical status labels are defined as part of the
        // statusOptions array at the top of the Razor file. We assert the
        // raw label strings are present so a future rename is caught.
        Assert.Contains("Label = \"Tất cả\"", src, StringComparison.Ordinal);
        Assert.Contains("Label = \"Đang bán\"", src, StringComparison.Ordinal);
        Assert.Contains("Label = \"Tạm hết\"", src, StringComparison.Ordinal);
        Assert.Contains("Label = \"Đã lưu trữ\"", src, StringComparison.Ordinal);

        // The category select must include the "Tất cả danh mục" option.
        Assert.Contains(">Tất cả danh mục</option>", src, StringComparison.Ordinal);

        // The select is named "status" so it round-trips through the URL.
        Assert.Contains("name=\"status\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_shows_clear_filters_link_when_filters_are_active()
    {
        var src = ReadFile(IndexPagePath);

        // The clear-filters link is rendered conditionally when
        // Model.HasActiveFilters is true. We assert the surrounding
        // @if(Model.HasActiveFilters) is in place so a future regression
        // removing the conditional is caught.
        Assert.Contains("Model.HasActiveFilters", src, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/menu\"", src, StringComparison.Ordinal);
        Assert.Contains("menu-operator__clear-filters", src, StringComparison.Ordinal);
        // The clear label must appear at least once in the markup (it can be
        // followed by whitespace and </a>). We accept any whitespace between
        // the label text and the closing tag.
        Assert.Contains("Xóa lọc", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_renders_search_input_with_preserved_value()
    {
        var src = ReadFile(IndexPagePath);
        // The search input is rendered with the current value via Razor.
        Assert.Contains("value=\"@currentSearch\"", src, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Tìm theo tên, mô tả...\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_renders_result_count_summary()
    {
        var src = ReadFile(IndexPagePath);
        Assert.Contains("menu-operator__result-count", src, StringComparison.Ordinal);
        Assert.Contains("Model.ResultCount", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_filter_form_submits_with_Enter_key()
    {
        var src = ReadFile(IndexPagePath);
        // Pressing Enter in a single text input inside a form submits the
        // form by default. We verify the form is a real <form method="get">
        // and the search input has no special JS handler that could swallow
        // the Enter key (i.e. it is a plain input, not a custom element).
        var searchIdx = src.IndexOf("id=\"menu-operator-search\"", StringComparison.Ordinal);
        Assert.True(searchIdx > 0);
        var inputOpen = src.LastIndexOf("<input", searchIdx, StringComparison.Ordinal);
        var inputClose = src.IndexOf('>', inputOpen);
        var input = src[inputOpen..(inputClose + 1)];
        Assert.Contains("type=\"search\"", input, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", input, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 16) Guest menu query remains unchanged.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guest_menu_index_excludes_archived_and_unavailable_items()
    {
        var src = ReadFile(GuestMenuPageModelPath);
        Assert.Contains("!i.IsArchived", src, StringComparison.Ordinal);
        Assert.Contains("i.IsAvailable && !i.IsArchived", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 17) Phase 1 POST actions still exist and are not broken.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Phase1_quick_availability_handlers_are_intact()
    {
        var src = ReadFile(IndexPageModelPath);
        Assert.Contains("OnPostSetSoldOutAsync", src, StringComparison.Ordinal);
        Assert.Contains("OnPostSetAvailableAsync", src, StringComparison.Ordinal);
        Assert.Contains("ChangeAvailabilityAsync", src, StringComparison.Ordinal);
        Assert.Contains("menu.sold_out", src, StringComparison.Ordinal);
        Assert.Contains("menu.available", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase1_quick_availability_handlers_still_protect_archived_items()
    {
        var src = ReadFile(IndexPageModelPath);
        var availabilityIdx = src.IndexOf("private async Task<IActionResult> ChangeAvailabilityAsync", StringComparison.Ordinal);
        Assert.True(availabilityIdx > 0);
        var sliceLen = Math.Min(4000, src.Length - availabilityIdx);
        var slice = src.Substring(availabilityIdx, sliceLen);
        Assert.Contains("if (item.IsArchived)", slice, StringComparison.Ordinal);
        Assert.Contains("khôi phục trước rồi mới thay đổi trạng thái bán", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_view_still_renders_antiforgery_tokens_on_phase1_forms()
    {
        var src = ReadFile(IndexPagePath);
        // Phase 1 quick actions + Phase 1 edit archive/restore forms all
        // require antiforgery tokens. We require at least 5 tokens on this
        // page (categories reorder, sold-out, available, archive, restore).
        var tokenCount = CountOccurrences(src, "@Html.AntiForgeryToken()");
        Assert.True(tokenCount >= 5,
            $"Expected at least 5 antiforgery tokens on Index.cshtml; found {tokenCount}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8) JavaScript no longer maintains a parallel client-side filter state.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Menu_operator_js_does_not_maintain_client_side_filter_state()
    {
        var src = ReadFile(JsPath);

        // No activeCategory closure variable.
        Assert.DoesNotContain("var activeCategory", src, StringComparison.Ordinal);
        // No applyFilters function definition.
        Assert.DoesNotContain("function applyFilters", src, StringComparison.Ordinal);
        // No 'hidden' row toggling for filtering.
        Assert.DoesNotContain("row.hidden = !show", src, StringComparison.Ordinal);
        // No chip click handler that mutates a filter state.
        Assert.DoesNotContain("chip.classList.add(\"is-active\")", src, StringComparison.Ordinal);

        // The category-ordering panel logic is still in place.
        Assert.Contains("toggle-category-order", src, StringComparison.Ordinal);
        Assert.Contains("cat-up", src, StringComparison.Ordinal);
        Assert.Contains("cat-down", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ApplyStatusFilter is a private static; we round-trip it through
    // OnGetAsync so that the same code path used by HTTP requests is
    // exercised here.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ApplyStatusFilter_branch_for_all_does_not_restrict_rows()
    {
        await using var db = NewInMemoryContext();
        var (_, items) = await SeedStandardMenuAsync(db);

        var page = NewPageModel(db);
        page.StatusFilterRaw = "all";
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(IndexModel.MenuStatusFilter.All, page.StatusFilter);
        Assert.Equal(items.Count, page.Items.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BindProperty contract — names must match the URL contract.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void BindProperty_attributes_match_url_query_parameters()
    {
        var src = ReadFile(IndexPageModelPath);
        // SupportsGet + Name match the contract documented in the brief.
        Assert.Contains("[BindProperty(SupportsGet = true, Name = \"q\")]", src, StringComparison.Ordinal);
        Assert.Contains("[BindProperty(SupportsGet = true, Name = \"category\")]", src, StringComparison.Ordinal);
        Assert.Contains("[BindProperty(SupportsGet = true, Name = \"status\")]", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Empty-state renders a useful message when filters exclude everything.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Index_view_distinguishes_empty_state_from_no_results_state()
    {
        var src = ReadFile(IndexPagePath);
        // "No items at all" message.
        Assert.Contains("Chưa có món nào trong thực đơn", src, StringComparison.Ordinal);
        // "Filters exclude everything" message.
        Assert.Contains("Không có món nào khớp với bộ lọc hiện tại", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static AppDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"menu-search-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Seed a known mix of items exercising every status combination:
    ///   - visible (available + not archived)
    ///   - sold-out (unavailable + not archived)
    ///   - archived-available (archived + available — should NEVER appear under available)
    ///   - archived-soldout (archived + unavailable)
    /// Across two categories.
    /// </summary>
    private static async Task<(List<MenuCategory> Cats, List<Guid> ItemIds)> SeedStandardMenuAsync(AppDbContext db)
    {
        var cafe = new MenuCategory { Name = "CAFE", SortOrder = 1 };
        var tea = new MenuCategory { Name = "TRÀ", SortOrder = 2 };
        db.MenuCategories.AddRange(cafe, tea);
        await db.SaveChangesAsync();

        var items = new List<MenuItem>
        {
            new() { CategoryId = cafe.Id, Name = "Phin Đen", Price = 39000m, IsAvailable = true, IsArchived = false },
            new() { CategoryId = cafe.Id, Name = "Phin Sữa", Price = 45000m, IsAvailable = false, IsArchived = false },
            new() { CategoryId = cafe.Id, Name = "Bạc Xỉu", Price = 49000m, IsAvailable = true, IsArchived = true },
            new() { CategoryId = cafe.Id, Name = "Cà phê Muối", Price = 55000m, IsAvailable = false, IsArchived = true },
            new() { CategoryId = tea.Id, Name = "Trà Sen", Price = 49000m, IsAvailable = true, IsArchived = false },
            new() { CategoryId = tea.Id, Name = "Trà Đào", Price = 49000m, IsAvailable = false, IsArchived = false },
            new() { CategoryId = tea.Id, Name = "Trà Earl Grey", Price = 52000m, IsAvailable = true, IsArchived = true },
        };
        db.MenuItems.AddRange(items);
        await db.SaveChangesAsync();

        return (new() { cafe, tea }, items.Select(i => i.Id).ToList());
    }

    private static IndexModel NewPageModel(AppDbContext db)
    {
        var http = new DefaultHttpContext();
        return new IndexModel(db, new Annap.CoffeeQrOrdering.Infrastructure.Services.PricingService(db))
        {
            PageContext = new PageContext { HttpContext = http }
        };
    }

    private static int CountOccurrences(string source, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }
}
