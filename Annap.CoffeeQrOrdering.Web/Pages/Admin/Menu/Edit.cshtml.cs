using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.Services;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;

[Authorize(Policy = "Staff")]
public sealed class EditModel(
    IApplicationDbContext db,
    IMenuImageStorage imageStorage,
    MenuMediaMaintenanceService mediaMaintenance) : PageModel
{
    [BindProperty]
    public MenuCurationFormModel Input { get; set; } = new();

    public Guid MenuItemId { get; private set; }

    public IReadOnlyList<MenuCategorySelectOption> CategoryOptions { get; private set; } = Array.Empty<MenuCategorySelectOption>();

    public string? CurrentHeroUrl { get; private set; }

    public string? CurrentDetailPosterUrl { get; private set; }

    public bool IsArchived { get; private set; }

    public MenuCurationPreviewVm Preview { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item is null)
            return NotFound();

        await LoadCategoriesAsync(cancellationToken);
        MapEntityToInput(item);
        MenuItemId = id;
        CurrentHeroUrl = item.ImageUrl;
        CurrentDetailPosterUrl = item.DetailPosterImagePath;
        IsArchived = item.IsArchived;
        Preview = BuildPreview(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
        MenuItemId = id;
        var existing = await db.MenuItems.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.ImageUrl, i.DetailPosterImagePath })
            .FirstOrDefaultAsync(cancellationToken);
        CurrentHeroUrl = existing?.ImageUrl;
        CurrentDetailPosterUrl = existing?.DetailPosterImagePath;
        Preview = BuildPreviewFromInput();

        var loaded = await db.MenuItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        IsArchived = loaded?.IsArchived ?? false;

        Input.Normalize();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            return Page();
        }

        if (!await db.MenuCategories.AnyAsync(c => c.Id == Input.CategoryId, cancellationToken))
        {
            ModelState.AddModelError(nameof(Input.CategoryId), "Choose a valid category.");
            return Page();
        }

        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        IsArchived = item.IsArchived;
        string? removedHeroUrl = null;
        string? removedPosterUrl = null;
        var previousHeroUrl = item.ImageUrl;
        var previousPosterUrl = item.DetailPosterImagePath;

        if (Input.RemoveHeroImage)
        {
            removedHeroUrl = item.ImageUrl;
            item.ImageUrl = null;
            CurrentHeroUrl = null;
        }

        if (Input.RemoveDetailPosterImage)
        {
            removedPosterUrl = item.DetailPosterImagePath;
            item.DetailPosterImagePath = null;
            CurrentDetailPosterUrl = null;
        }

        try
        {
            if (Input.HeroImage is { Length: > 0 })
            {
                var replaceDetailFromHero = !Input.RemoveDetailPosterImage && Input.DetailPosterImage is not { Length: > 0 };
                var url = await imageStorage.SaveHeroAsync(Input.HeroImage, item.Id, cancellationToken);
                if (url is not null)
                {
                    item.ImageUrl = url;
                    CurrentHeroUrl = url;

                    if (replaceDetailFromHero)
                    {
                        var posterUrl = await imageStorage.SavePosterAsync(
                            Input.HeroImage,
                            item.Id,
                            cancellationToken);
                        item.DetailPosterImagePath = posterUrl ?? url;
                        CurrentDetailPosterUrl = item.DetailPosterImagePath;
                    }
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(Input.HeroImage), ex.Message);
            var fresh = await db.MenuItems
                .AsNoTracking()
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (fresh is not null)
            {
                MapEntityToInput(fresh);
                CurrentHeroUrl = fresh.ImageUrl;
                CurrentDetailPosterUrl = fresh.DetailPosterImagePath;
                IsArchived = fresh.IsArchived;
                Preview = BuildPreview(fresh);
            }
            return Page();
        }

        try
        {
            if (Input.DetailPosterImage is { Length: > 0 })
            {
                var url = await imageStorage.SavePosterAsync(Input.DetailPosterImage, item.Id, cancellationToken);
                if (url is not null)
                {
                    item.DetailPosterImagePath = url;
                    CurrentDetailPosterUrl = url;
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(Input.DetailPosterImage), ex.Message);
            var fresh = await db.MenuItems
                .AsNoTracking()
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (fresh is not null)
            {
                MapEntityToInput(fresh);
                CurrentHeroUrl = fresh.ImageUrl;
                CurrentDetailPosterUrl = fresh.DetailPosterImagePath;
                IsArchived = fresh.IsArchived;
                Preview = BuildPreview(fresh);
            }
            return Page();
        }

        ApplyInputToEntity(item);
        var actor = User.Identity?.Name?.Trim();
        await OperationalAudit.AppendAsync(db, "menu.updated", actor, null,
            $"menuItemId={item.Id};name={Summarize(item.Name)}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (removedHeroUrl is not null && item.ImageUrl is null)
            await imageStorage.DeleteHeroAsync(item.Id, removedHeroUrl, cancellationToken);
        else if (MenuImageStorage.ShouldDeleteSupersededUrl(previousHeroUrl, item.ImageUrl))
            await imageStorage.DeleteHeroAsync(item.Id, previousHeroUrl, cancellationToken);

        if (removedPosterUrl is not null && item.DetailPosterImagePath is null)
            await imageStorage.DeletePosterAsync(item.Id, removedPosterUrl, cancellationToken);
        else if (MenuImageStorage.ShouldDeleteSupersededUrl(previousPosterUrl, item.DetailPosterImagePath))
            await imageStorage.DeletePosterAsync(item.Id, previousPosterUrl, cancellationToken);

        TempData["MenuCurationToast"] = "Đã lưu thay đổi.";
        return RedirectToPage("/Admin/Menu/Index");
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        item.IsArchived = true;
        var archivedHeroUrl = item.ImageUrl;
        var archivedPosterUrl = item.DetailPosterImagePath;
        item.ImageUrl = null;
        item.DetailPosterImagePath = null;
        var actor = User.Identity?.Name?.Trim();
        await OperationalAudit.AppendAsync(db, "menu.archived", actor, null,
            $"menuItemId={item.Id};name={Summarize(item.Name)}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await mediaMaintenance.PurgeManagedAssetsForItemAsync(
            item.Id,
            archivedHeroUrl,
            archivedPosterUrl,
            cancellationToken);
        TempData["MenuCurationToast"] = "Đã lưu trữ món.";
        return RedirectToPage("/Admin/Menu/Index");
    }

    public async Task<IActionResult> OnPostRestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        item.IsArchived = false;
        var actor = User.Identity?.Name?.Trim();
        await OperationalAudit.AppendAsync(db, "menu.restored", actor, null,
            $"menuItemId={item.Id};name={Summarize(item.Name)}", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        TempData["MenuCurationToast"] = "Đã khôi phục món.";
        return RedirectToPage("/Admin/Menu/Index");
    }

    private MenuCurationPreviewVm BuildPreview(MenuItem item)
    {
        var cat = item.Category?.Name ?? "—";
        var resolved = MenuMediaResolver.TryResolveCardImageUrl(null, null, item.ImageUrl, null, item.Name, cat);
        var hero = string.IsNullOrWhiteSpace(resolved) ? null : resolved;
        CurrentDetailPosterUrl = item.DetailPosterImagePath;
        return new MenuCurationPreviewVm(
            item.Name,
            item.Subtitle,
            item.Price,
            cat,
            item.IsAvailable,
            hero,
            resolved ?? "");
    }

    private MenuCurationPreviewVm BuildPreviewFromInput()
    {
        var catName = CategoryOptions.FirstOrDefault(c => c.Id == Input.CategoryId)?.Name
                      ?? CategoryOptions.FirstOrDefault()?.Name ?? "—";
        var hero = string.IsNullOrWhiteSpace(CurrentHeroUrl) ? null : CurrentHeroUrl;
        if (Input.RemoveHeroImage)
            hero = null;
        var resolved = MenuMediaResolver.TryResolveCardImageUrl(null, null, hero, null, Input.Name, catName);
        return new MenuCurationPreviewVm(
            string.IsNullOrWhiteSpace(Input.Name) ? "Tên món" : Input.Name.Trim(),
            NullIfWhite(Input.Subtitle),
            Input.Price,
            catName,
            Input.IsAvailable,
            hero,
            resolved ?? "");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        CategoryOptions = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new MenuCategorySelectOption(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    private void MapEntityToInput(MenuItem item)
        => Input = MenuItemProjections.ToAdminForm(item);

    private void ApplyInputToEntity(MenuItem item)
        => MenuItemProjections.ApplyFormToEntity(Input, item);

    private static string? NullIfWhite(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Summarize(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length <= 160) return n;
        return n[..157] + "…";
    }
}