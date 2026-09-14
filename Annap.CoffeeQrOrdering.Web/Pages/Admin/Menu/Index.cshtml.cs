using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    // ─────────────────────────────────────────────────────────────────────────
    // Filter / search query parameters (Phase 2 — server-side filtering).
    //
    // Query string contract:
    //   q        search text (Name | Subtitle | Description | CatalogKey)
    //   category category ID (Guid); missing/invalid → no category filter
    //   status   one of: all | available | soldout | archived (default: all)
    //
    // Parameters are bound from the URL query string so reload, browser
    // back/forward, and shared links all reproduce the same result set.
    // JavaScript is not the source of truth — invalid values fall back
    // safely to the unfiltered baseline.
    // ─────────────────────────────────────────────────────────────────────────

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? SearchQuery { get; set; }

    [BindProperty(SupportsGet = true, Name = "category")]
    public Guid? CategoryFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilterRaw { get; set; }

    /// <summary>Normalized status filter — invalid values resolve to <see cref="MenuStatusFilter.All"/>.</summary>
    public MenuStatusFilter StatusFilter { get; private set; } = MenuStatusFilter.All;

    /// <summary>Trimmed search text (null when blank).</summary>
    public string? NormalizedSearch { get; private set; }

    /// <summary>True when at least one filter URL parameter is non-blank — used to decide whether
    /// to render the "Clear filters" affordance and the active-filter chips.</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchQuery)
        || CategoryFilter.HasValue
        || !string.IsNullOrWhiteSpace(StatusFilterRaw);

    public int ResultCount => Items.Count;

    public IReadOnlyList<MenuRowVm> Items { get; private set; } = Array.Empty<MenuRowVm>();

    public IReadOnlyList<CategoryOptionVm> Categories { get; private set; } = Array.Empty<CategoryOptionVm>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Resolve filter values first so we can apply them to the database
        // query before any data is materialized.
        StatusFilter = ParseStatus(StatusFilterRaw);
        NormalizedSearch = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim();

        // Categories are still loaded in full — they're a small control set,
        // and the category-filter dropdown must always show every category so
        // staff can switch between them.
        Categories = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryOptionVm(c.Id, c.Name, c.SortOrder))
            .ToListAsync(cancellationToken);

        // Guard: if the requested category no longer exists, treat the filter
        // as inactive rather than rendering an empty list silently. This keeps
        // bookmarks from a deleted category useful.
        if (CategoryFilter.HasValue && !Categories.Any(c => c.Id == CategoryFilter.Value))
        {
            CategoryFilter = null;
        }

        // Build the filtered item query. EVERY filter is applied as a WHERE
        // clause in the database; ToListAsync materializes only the rows that
        // match. Category navigation is included once at the end so the row
        // VM can render the category name — that is the only navigation
        // loaded, so we do not pull unrelated graphs.
        IQueryable<MenuItem> itemsQuery = db.MenuItems.AsNoTracking();

        itemsQuery = ApplyStatusFilter(itemsQuery, StatusFilter);

        if (CategoryFilter.HasValue)
        {
            var cat = CategoryFilter.Value;
            itemsQuery = itemsQuery.Where(i => i.CategoryId == cat);
        }

        if (!string.IsNullOrEmpty(NormalizedSearch))
        {
            // EF.Functions.Like with a single '%term%' pattern matches the
            // project's existing pattern. The Npgsql/PostgreSQL text provider
            // applies LIKE case-insensitively against a text-typed column by
            // default — see the production DATABASE_URL configuration.
            var pattern = $"%{NormalizedSearch}%";
            itemsQuery = itemsQuery.Where(i =>
                EF.Functions.Like(i.Name, pattern)
                || (i.Subtitle != null && EF.Functions.Like(i.Subtitle, pattern))
                || (i.Description != null && EF.Functions.Like(i.Description, pattern))
                || (i.CatalogKey != null && EF.Functions.Like(i.CatalogKey, pattern)));
        }

        var rows = await itemsQuery
            .Include(i => i.Category)
            .OrderBy(i => i.IsArchived)
            .ThenBy(i => i.DisplaySortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        Items = rows.Select(BuildRow).ToList();
    }

    private static IQueryable<MenuItem> ApplyStatusFilter(IQueryable<MenuItem> q, MenuStatusFilter status) =>
        status switch
        {
            // Available: visible to guests, ready to order.
            // We deliberately do NOT include archived here even when IsAvailable
            // happens to be true — archived rows are a separate operator view.
            MenuStatusFilter.Available => q.Where(i => !i.IsArchived && i.IsAvailable),
            // Sold out: still on the menu (not archived) but currently unorderable.
            MenuStatusFilter.SoldOut => q.Where(i => !i.IsArchived && !i.IsAvailable),
            // Archived: hidden from the guest menu entirely. Archived takes
            // precedence over availability — an archived item that is somehow
            // flagged as IsAvailable is still archived, not "available".
            MenuStatusFilter.Archived => q.Where(i => i.IsArchived),
            // All (default): no filter applied.
            _ => q
        };

    private static MenuStatusFilter ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return MenuStatusFilter.All;
        return raw.Trim().ToLowerInvariant() switch
        {
            "available" => MenuStatusFilter.Available,
            "soldout" or "sold-out" or "sold_out" => MenuStatusFilter.SoldOut,
            "archived" => MenuStatusFilter.Archived,
            _ => MenuStatusFilter.All
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quick availability actions (Phase 1 archive/restore safety)
    //
    // These two handlers only mutate IsAvailable. They NEVER mutate IsArchived,
    // image URLs, image metadata, or any other field. They are idempotent:
    // invoking them when the item is already in the target state is a no-op
    // (no audit row, no toast saying "changed").
    // ─────────────────────────────────────────────────────────────────────────

    public Task<IActionResult> OnPostSetSoldOutAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAvailabilityAsync(id, newIsAvailable: false, "menu.sold_out", cancellationToken);

    public Task<IActionResult> OnPostSetAvailableAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAvailabilityAsync(id, newIsAvailable: true, "menu.available", cancellationToken);

    private async Task<IActionResult> ChangeAvailabilityAsync(
        Guid id,
        bool newIsAvailable,
        string actionKind,
        CancellationToken cancellationToken)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        // Hard rule: archived items cannot be flipped to available via this path.
        // Quick availability actions are only meaningful when the item is visible
        // to staff on the operator list. Restoring is a separate explicit action
        // that lives in the dangerous-zone section.
        if (item.IsArchived)
        {
            TempData["MenuCurationToast"] =
                "Món đang được lưu trữ — khôi phục trước rồi mới thay đổi trạng thái bán.";
            return RedirectToPage("/Admin/Menu/Index");
        }

        // Idempotent: do not emit a duplicate audit row when the state already
        // matches the requested target.
        if (item.IsAvailable == newIsAvailable)
        {
            TempData["MenuCurationToast"] = newIsAvailable
                ? "Món đã đang được bán."
                : "Món đã ở trạng thái tạm hết.";
            return RedirectToPage("/Admin/Menu/Index");
        }

        var previousIsAvailable = item.IsAvailable;
        item.IsAvailable = newIsAvailable;

        var actor = User.Identity?.Name?.Trim();
        await OperationalAudit.AppendAsync(
            db,
            actionKind,
            actor,
            null,
            BuildAvailabilitySummary(
                item.Id,
                item.Name,
                previousIsAvailable,
                newIsAvailable,
                item.IsArchived),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        TempData["MenuCurationToast"] = newIsAvailable
            ? "Đã bật bán cho món."
            : "Đã tạm hết món.";
        return RedirectToPage("/Admin/Menu/Index");
    }

    private static string BuildAvailabilitySummary(
        Guid menuItemId,
        string? menuItemName,
        bool previousIsAvailable,
        bool newIsAvailable,
        bool isArchived)
    {
        var name = (menuItemName ?? "").Trim();
        if (name.Length > 160)
            name = name[..157] + "…";

        return string.Concat(
            "menuItemId=", menuItemId.ToString("D"),
            ";name=", name,
            ";prevIsAvailable=", previousIsAvailable ? "true" : "false",
            ";newIsAvailable=", newIsAvailable ? "true" : "false",
            ";isArchived=", isArchived ? "true" : "false");
    }

    private static MenuRowVm BuildRow(MenuItem item)
    {
        var catName = item.Category?.Name ?? "—";
        var preview = MenuMediaResolver.ResolveCardImageUrl(item, catName);
        return new MenuRowVm(
            item.Id,
            item.Name,
            item.Subtitle,
            item.CategoryId,
            catName,
            item.Price,
            item.IsAvailable,
            item.IsFeatured,
            item.IsSignature,
            item.IsArchived,
            preview);
    }

    /// <summary>Canonical status values for the admin menu filter UI.</summary>
    public enum MenuStatusFilter
    {
        All = 0,
        Available = 1,
        SoldOut = 2,
        Archived = 3
    }

    public sealed record CategoryOptionVm(Guid Id, string Name, int SortOrder);

    public sealed record MenuRowVm(
        Guid Id,
        string Name,
        string? Subtitle,
        Guid CategoryId,
        string CategoryName,
        decimal Price,
        bool IsAvailable,
        bool IsFeatured,
        bool IsSignature,
        bool IsArchived,
        string PreviewImageUrl);
}
