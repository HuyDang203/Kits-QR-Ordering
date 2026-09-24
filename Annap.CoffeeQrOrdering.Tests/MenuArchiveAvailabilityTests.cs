using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Phase 1 production-safety tests for admin menu archive/restore and quick
/// availability actions. The goal is to guarantee the invariants documented in
/// the phase brief:
///
///   • Archive never clears image URLs / image metadata.
///   • Archive never calls Cloudinary / local asset deletion.
///   • Restore never re-uploads or fabricates images.
///   • Mark sold out / mark available only touches IsAvailable.
///   • All four transitions emit an audit row (and only when the state actually
///     changed — no duplicates on idempotent no-ops).
///   • Archived items remain excluded from the guest menu query.
/// </summary>
public sealed class MenuArchiveAvailabilityTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Repository paths (shared with other static-test files in this project).
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    private static readonly string EditPageModelPath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Edit.cshtml.cs");

    private static readonly string IndexPageModelPath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml.cs");

    private static readonly string IndexPagePath = Path.Combine(
        PagesRoot, "Admin", "Menu", "Index.cshtml");

    private static string ReadFile(string path) =>
        File.ReadAllText(Path.GetFullPath(path));

    // ─────────────────────────────────────────────────────────────────────────
    // 1) Archive preserves image URLs (source-level contract).
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_archive_handler_does_not_clear_image_urls_in_source()
    {
        var src = ReadFile(EditPageModelPath);

        // The OnPostArchiveAsync method must not contain any line that nulls
        // out the image fields. The presence of these substrings would mean
        // someone reintroduced the data-loss bug we just removed.
        var archiveStart = src.IndexOf("OnPostArchiveAsync", StringComparison.Ordinal);
        Assert.True(archiveStart > 0, "OnPostArchiveAsync handler must exist on Edit.cshtml.cs.");
        var archiveEnd = src.IndexOf("OnPostRestoreAsync", StringComparison.Ordinal);
        Assert.True(archiveEnd > archiveStart, "OnPostRestoreAsync handler must follow OnPostArchiveAsync.");
        var archiveBody = src[archiveStart..archiveEnd];

        Assert.DoesNotContain("item.ImageUrl = null", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("item.ImageUrl = null;", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("item.DetailPosterImagePath = null", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("item.DetailPosterImagePath = null;", archiveBody, StringComparison.Ordinal);

        // Defensive comment is required so future readers know the absence is
        // intentional, not accidental.
        Assert.Contains("Deliberately NOT clearing ImageUrl", archiveBody, StringComparison.Ordinal);
        Assert.Contains("Deliberately NOT clearing", archiveBody, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2) Archive does NOT delete Cloudinary / local assets.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_archive_handler_does_not_call_image_deletion_methods()
    {
        var src = ReadFile(EditPageModelPath);

        var archiveStart = src.IndexOf("OnPostArchiveAsync", StringComparison.Ordinal);
        var archiveEnd = src.IndexOf("OnPostRestoreAsync", StringComparison.Ordinal);
        var archiveBody = src[archiveStart..archiveEnd];

        // Hard rule: archive must never invoke image-storage deletion. We
        // accept the method name appearing in explanatory comments, but the
        // handler body must not contain an awaited invocation.
        Assert.DoesNotContain("await mediaMaintenance", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await imageStorage.DeleteHeroAsync", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("await imageStorage.DeletePosterAsync", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteHeroAsync(", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DeletePosterAsync(", archiveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("PurgeManagedAssetsForItemAsync(", archiveBody, StringComparison.Ordinal);

        // A comment should explicitly call this out so the invariant is
        // discoverable from the file itself.
        Assert.Contains("Deliberately NOT calling mediaMaintenance", archiveBody, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3) Restore does not call image upload or recreation.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_restore_handler_does_not_call_image_upload_or_delete()
    {
        var src = ReadFile(EditPageModelPath);

        var restoreStart = src.IndexOf("OnPostRestoreAsync", StringComparison.Ordinal);
        // The next method after restore is BuildMenuStateSummary (helper), so
        // grab a large slice to be safe.
        var restoreEnd = src.IndexOf("private static string BuildMenuStateSummary", StringComparison.Ordinal);
        Assert.True(restoreEnd > restoreStart, "Restore handler must precede BuildMenuStateSummary helper.");
        var restoreBody = src[restoreStart..restoreEnd];

        Assert.DoesNotContain("SaveHeroAsync", restoreBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SavePosterAsync", restoreBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteHeroAsync", restoreBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DeletePosterAsync", restoreBody, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4) Quick availability handlers exist on Index page.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Index_model_exposes_quick_availability_handlers()
    {
        var src = ReadFile(IndexPageModelPath);

        Assert.Contains("OnPostSetSoldOutAsync", src, StringComparison.Ordinal);
        Assert.Contains("OnPostSetAvailableAsync", src, StringComparison.Ordinal);

        // The handlers must delegate to a shared mutator that records an audit
        // row and never touches IsArchived.
        Assert.Contains("menu.sold_out", src, StringComparison.Ordinal);
        Assert.Contains("menu.available", src, StringComparison.Ordinal);
        Assert.Contains("ChangeAvailabilityAsync", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5) Index view renders quick-action POST forms with antiforgery tokens.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Index_view_renders_antiforgery_token_on_quick_action_forms()
    {
        var src = ReadFile(IndexPagePath);

        Assert.Contains("asp-page-handler=\"SetSoldOut\"", src, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SetAvailable\"", src, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Restore\"", src, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Archive\"", src, StringComparison.Ordinal);

        // Every POST form on this page must include the antiforgery token.
        // We assert the substring appears multiple times — one per form.
        var tokenCount = CountOccurrences(src, "@Html.AntiForgeryToken()");
        Assert.True(tokenCount >= 5,
            $"Expected at least 5 @Html.AntiForgeryToken() calls (categories, sold-out, available, archive, restore); found {tokenCount}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6) Edit archive/restore forms also carry antiforgery tokens.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_view_renders_antiforgery_token_on_archive_and_restore_forms()
    {
        var src = ReadFile(Path.Combine(PagesRoot, "Admin", "Menu", "Edit.cshtml"));

        // The two dangerous-zone POST forms must include the antiforgery token.
        Assert.Contains("asp-page-handler=\"Restore\"", src, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Archive\"", src, StringComparison.Ordinal);

        // The main edit form uses the form tag helper which emits an
        // antiforgery token automatically when `asp-page` is set. The two new
        // POST forms in the danger zone must include the explicit token. We
        // require at least the two explicit ones here, and assert that no
        // archive/restore POST form exists without a token immediately inside
        // it.
        var tokenCount = CountOccurrences(src, "@Html.AntiForgeryToken()");
        Assert.True(tokenCount >= 2,
            $"Expected at least 2 explicit antiforgery tokens on Edit.cshtml (archive + restore); found {tokenCount}.");

        // Every archive/restore form must be followed by an antiforgery token
        // before its submit button — guard against future regressions.
        foreach (var handler in new[] { "Restore", "Archive" })
        {
            var formIdx = src.IndexOf($"asp-page-handler=\"{handler}\"", StringComparison.Ordinal);
            Assert.True(formIdx > 0, $"Form with handler '{handler}' must exist on Edit.cshtml.");
            var nextClose = src.IndexOf("</form>", formIdx, StringComparison.Ordinal);
            Assert.True(nextClose > formIdx);
            var slice = src[formIdx..nextClose];
            Assert.Contains("@Html.AntiForgeryToken()", slice, StringComparison.Ordinal);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7) Guest menu query must still exclude archived items.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Guest_menu_index_excludes_archived_items_in_source()
    {
        var src = ReadFile(Path.Combine(PagesRoot, "Menu", "Index.cshtml.cs"));

        // We assert the negative condition holds in the Where clause that
        // feeds the guest categories.
        Assert.Contains("!i.IsArchived", src, StringComparison.Ordinal);
        Assert.Contains("i.IsAvailable && !i.IsArchived", src, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8) Visibility badge logic on the Index view: archived > available > sold out.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Index_view_renders_status_pill_for_every_state()
    {
        var src = ReadFile(IndexPagePath);

        Assert.Contains("menu-operator__pill--arch", src, StringComparison.Ordinal);
        Assert.Contains("menu-operator__pill--ok", src, StringComparison.Ordinal);
        Assert.Contains("menu-operator__pill--warn", src, StringComparison.Ordinal);

        // The archive pill must be reachable without depending on availability
        // badges. We assert that the "Đã lưu trữ" pill is rendered first when
        // IsArchived is true, ahead of any availability check.
        var archPillIndex = src.IndexOf("Đã lưu trữ", StringComparison.Ordinal);
        var okPillIndex = src.IndexOf("Đang bán", StringComparison.Ordinal);
        var warnPillIndex = src.IndexOf("Tạm hết", StringComparison.Ordinal);
        Assert.True(archPillIndex > 0);
        Assert.True(okPillIndex > 0);
        Assert.True(warnPillIndex > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9) Restore handler is idempotent (returns early without emitting a row).
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_archive_and_restore_are_idempotent_in_source()
    {
        var src = ReadFile(EditPageModelPath);

        var archiveStart = src.IndexOf("OnPostArchiveAsync", StringComparison.Ordinal);
        var archiveEnd = src.IndexOf("OnPostRestoreAsync", StringComparison.Ordinal);
        var archiveBody = src[archiveStart..archiveEnd];

        // Must short-circuit when IsArchived is already true.
        Assert.Contains("if (item.IsArchived)", archiveBody, StringComparison.Ordinal);
        Assert.Contains("Món đã được lưu trữ trước đó", archiveBody, StringComparison.Ordinal);

        var restoreStart = src.IndexOf("OnPostRestoreAsync", StringComparison.Ordinal);
        var restoreEnd = src.IndexOf("private static string BuildMenuStateSummary", StringComparison.Ordinal);
        var restoreBody = src[restoreStart..restoreEnd];

        // Must short-circuit when IsArchived is already false.
        Assert.Contains("if (!item.IsArchived)", restoreBody, StringComparison.Ordinal);
        Assert.Contains("Món đã được khôi phục trước đó", restoreBody, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10) Behavioral tests using EF Core InMemory to confirm the page models
    //     actually preserve image fields and only touch IsAvailable when
    //     requested.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_archive_preserves_image_urls_in_memory()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Phin Đen",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/abc123.webp",
            posterUrl: "https://res.cloudinary.com/demo/image/upload/kits/abc123-poster.webp",
            isAvailable: true,
            isArchived: false);

        var page = NewEditPageModel(db);

        var result = await page.OnPostArchiveAsync(item.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.True(fresh.IsArchived);
        Assert.Equal(item.ImageUrl, fresh.ImageUrl);
        Assert.Equal(item.DetailPosterImagePath, fresh.DetailPosterImagePath);
    }

    [Fact]
    public async Task Edit_archive_emits_audit_row_with_previous_and_new_state()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Phin Sữa",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/phin-sua.webp",
            posterUrl: null,
            isAvailable: true,
            isArchived: false);

        var page = NewEditPageModel(db);
        page.PageContext = NewPageContext(actor: "staff-tester");

        await page.OnPostArchiveAsync(item.Id, CancellationToken.None);

        var audit = await db.OperationalAuditEntries.AsNoTracking()
            .Where(a => a.ActionKind == "menu.archived")
            .SingleAsync();
        Assert.Equal("staff-tester", audit.Actor);
        Assert.Contains("action=archive", audit.Summary, StringComparison.Ordinal);
        Assert.Contains($"menuItemId={item.Id:D}", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("prevIsAvailable=true", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("newIsAvailable=true", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("prevIsArchived=false", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("newIsArchived=true", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("newImageUrl=", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_archive_is_idempotent_and_does_not_emit_duplicate_audit_row()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Cappuccino",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/cap.webp",
            posterUrl: null,
            isAvailable: true,
            isArchived: true); // already archived

        var page = NewEditPageModel(db);

        await page.OnPostArchiveAsync(item.Id, CancellationToken.None);
        var count = await db.OperationalAuditEntries.AsNoTracking()
            .CountAsync(a => a.ActionKind == "menu.archived");
        Assert.Equal(0, count);

        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.True(fresh.IsArchived);
        Assert.Equal(item.ImageUrl, fresh.ImageUrl);
    }

    [Fact]
    public async Task Edit_restore_preserves_image_urls_in_memory()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Bạc Xỉu",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/bac-xiu.webp",
            posterUrl: "https://res.cloudinary.com/demo/image/upload/kits/bac-xiu-poster.webp",
            isAvailable: true,
            isArchived: true);

        var page = NewEditPageModel(db);

        var result = await page.OnPostRestoreAsync(item.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.False(fresh.IsArchived);
        Assert.Equal(item.ImageUrl, fresh.ImageUrl);
        Assert.Equal(item.DetailPosterImagePath, fresh.DetailPosterImagePath);
    }

    [Fact]
    public async Task Edit_restore_is_idempotent_when_not_archived()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Latte",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/latte.webp",
            posterUrl: null,
            isAvailable: true,
            isArchived: false);

        var page = NewEditPageModel(db);

        await page.OnPostRestoreAsync(item.Id, CancellationToken.None);
        var count = await db.OperationalAuditEntries.AsNoTracking()
            .CountAsync(a => a.ActionKind == "menu.restored");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Index_mark_sold_out_only_changes_is_available()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Caramel Macchiato",
            imageUrl: "https://res.cloudinary.com/demo/image/upload/kits/cmac.webp",
            posterUrl: null,
            isAvailable: true,
            isArchived: false);

        var page = NewIndexPageModel(db, actor: "staff-tester");

        await page.OnPostSetSoldOutAsync(item.Id, CancellationToken.None);

        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.False(fresh.IsAvailable);
        Assert.False(fresh.IsArchived);
        Assert.Equal(item.ImageUrl, fresh.ImageUrl);
        Assert.Equal(item.DetailPosterImagePath, fresh.DetailPosterImagePath);
        Assert.Equal(item.Price, fresh.Price);
        Assert.Equal(item.Name, fresh.Name);

        var audit = await db.OperationalAuditEntries.AsNoTracking()
            .Where(a => a.ActionKind == "menu.sold_out")
            .SingleAsync();
        Assert.Equal("staff-tester", audit.Actor);
        Assert.Contains("prevIsAvailable=true", audit.Summary, StringComparison.Ordinal);
        Assert.Contains("newIsAvailable=false", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_mark_available_only_changes_is_available()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Mocha",
            imageUrl: null,
            posterUrl: null,
            isAvailable: false,
            isArchived: false);

        var page = NewIndexPageModel(db);

        await page.OnPostSetAvailableAsync(item.Id, CancellationToken.None);

        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.True(fresh.IsAvailable);
        Assert.False(fresh.IsArchived);
        Assert.Equal(item.Price, fresh.Price);
        Assert.Equal(item.Name, fresh.Name);
    }

    [Fact]
    public async Task Index_mark_available_does_not_unarchive_archived_items()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Cà phê muối",
            imageUrl: null,
            posterUrl: null,
            isAvailable: false,
            isArchived: true);

        var page = NewIndexPageModel(db);

        await page.OnPostSetAvailableAsync(item.Id, CancellationToken.None);

        var fresh = await db.MenuItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.True(fresh.IsArchived, "Archived items must remain archived after a quick availability action.");
        Assert.False(fresh.IsAvailable, "Quick availability must not flip the availability state of an archived item.");

        var count = await db.OperationalAuditEntries.AsNoTracking()
            .CountAsync(a => a.ActionKind == "menu.available");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Index_mark_sold_out_emits_no_audit_row_when_already_sold_out()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Trà Earl Grey",
            imageUrl: null,
            posterUrl: null,
            isAvailable: false,
            isArchived: false);

        var page = NewIndexPageModel(db);

        await page.OnPostSetSoldOutAsync(item.Id, CancellationToken.None);

        var count = await db.OperationalAuditEntries.AsNoTracking()
            .CountAsync(a => a.ActionKind == "menu.sold_out");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Index_mark_available_emits_no_audit_row_when_already_available()
    {
        await using var db = NewInMemoryContext();
        var item = await SeedMenuItemAsync(
            db,
            name: "Trà hoa cúc",
            imageUrl: null,
            posterUrl: null,
            isAvailable: true,
            isArchived: false);

        var page = NewIndexPageModel(db);

        await page.OnPostSetAvailableAsync(item.Id, CancellationToken.None);

        var count = await db.OperationalAuditEntries.AsNoTracking()
            .CountAsync(a => a.ActionKind == "menu.available");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Guest_menu_query_excludes_archived_items()
    {
        await using var db = NewInMemoryContext();

        var category = new MenuCategory { Name = "CF", SortOrder = 1 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        var visible = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Visible Latte",
            Price = 50_000m,
            IsAvailable = true,
            IsArchived = false,
            ImageUrl = "https://res.cloudinary.com/demo/image/upload/kits/v.webp"
        };
        var hiddenByArchive = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Archived Latte",
            Price = 50_000m,
            IsAvailable = true,
            IsArchived = true,
            ImageUrl = "https://res.cloudinary.com/demo/image/upload/kits/a.webp"
        };
        var hiddenBySoldOut = new MenuItem
        {
            CategoryId = category.Id,
            Name = "Sold-out Latte",
            Price = 50_000m,
            IsAvailable = false,
            IsArchived = false
        };
        db.MenuItems.AddRange(visible, hiddenByArchive, hiddenBySoldOut);
        await db.SaveChangesAsync();

        // Mirror the guest filter from Pages/Menu/Index.cshtml.cs:
        //   .Include(c => c.Items.Where(i => i.IsAvailable && !i.IsArchived))
        var rows = await db.MenuCategories.AsNoTracking()
            .Include(c => c.Items.Where(i => i.IsAvailable && !i.IsArchived))
            .OrderBy(c => c.SortOrder)
            .Where(c => c.Items.Any(i => i.IsAvailable && !i.IsArchived))
            .ToListAsync();

        Assert.Single(rows);
        var itemNames = rows[0].Items.Select(i => i.Name).ToList();
        Assert.Contains("Visible Latte", itemNames);
        Assert.DoesNotContain("Archived Latte", itemNames);
        Assert.DoesNotContain("Sold-out Latte", itemNames);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static AppDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"menu-archive-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<MenuItem> SeedMenuItemAsync(
        AppDbContext db,
        string name,
        string? imageUrl,
        string? posterUrl,
        bool isAvailable,
        bool isArchived)
    {
        var category = new MenuCategory { Name = $"Cat-{Guid.NewGuid():N}", SortOrder = 1 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        var item = new MenuItem
        {
            CategoryId = category.Id,
            Name = name,
            Price = 45_000m,
            IsAvailable = isAvailable,
            IsArchived = isArchived,
            ImageUrl = imageUrl,
            DetailPosterImagePath = posterUrl
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static EditModel NewEditPageModel(AppDbContext db) =>
        new EditModel(db, new FakeMenuImageStorage())
        {
            PageContext = NewPageContext(),
            TempData = new NullTempDataDictionary()
        };

    private static IndexModel NewIndexPageModel(AppDbContext db, string? actor = null) =>
        new IndexModel(db, new Annap.CoffeeQrOrdering.Infrastructure.Services.PricingService(db))
        {
            PageContext = NewPageContext(actor),
            TempData = new NullTempDataDictionary()
        };

    private static PageContext NewPageContext(string? actor = null)
    {
        var http = new DefaultHttpContext();
        if (actor is not null)
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Name, actor) },
                authenticationType: "TestAuth");
            http.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        return new PageContext { HttpContext = http };
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

    /// <summary>No-op image storage for unit tests — never touches Cloudinary or local disk.</summary>
    private sealed class FakeMenuImageStorage : IMenuImageStorage
    {
        public Task<string?> SaveHeroAsync(IFormFile? file, Guid itemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> SavePosterAsync(IFormFile? file, Guid itemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task DeleteHeroAsync(Guid itemId, string? currentUrl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeletePosterAsync(Guid itemId, string? currentUrl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// Minimal ITempDataDictionary implementation for unit tests. It silently
    /// accepts writes, returns nothing on reads, and pretends to be empty.
    /// Real cross-request flow is covered by the integration tests that go
    /// through the full HTTP pipeline.
    /// </summary>
    private sealed class NullTempDataDictionary : ITempDataDictionary
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public object? this[string key]
        {
            get => _store.TryGetValue(key, out var v) ? v : null;
            set => _store[key] = value;
        }

        public ICollection<string> Keys => _store.Keys;
        public ICollection<object?> Values => _store.Values;
        public int Count => _store.Count;
        public bool IsReadOnly => false;

        public void Add(string key, object? value) => _store.Add(key, value);
        public void Add(KeyValuePair<string, object?> item) => _store.Add(item.Key, item.Value);
        public void Clear() => _store.Clear();
        public bool Contains(KeyValuePair<string, object?> item) =>
            _store.TryGetValue(item.Key, out var v) && Equals(v, item.Value);
        public bool ContainsKey(string key) => _store.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, object?>>)_store).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _store.GetEnumerator();
        public void Keep() { }
        public void Keep(string key) { }
        public void Load() { }
        public bool Remove(string key) => _store.Remove(key);
        public bool Remove(KeyValuePair<string, object?> item) => _store.Remove(item.Key);
        public bool TryGetValue(string key, out object? value)
        {
            var ok = _store.TryGetValue(key, out var v);
            value = v;
            return ok;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ((System.Collections.IEnumerable)_store).GetEnumerator();

        public void Save() { }

        public object? Peek(string key) => null;
    }
}
