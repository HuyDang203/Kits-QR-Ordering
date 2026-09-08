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
public sealed class CreateModel(IApplicationDbContext db, IMenuImageStorage imageStorage) : PageModel
{
    [BindProperty]
    public MenuCurationFormModel Input { get; set; } = new();

    public IReadOnlyList<MenuCategorySelectOption> CategoryOptions { get; private set; } = Array.Empty<MenuCategorySelectOption>();

    public string? CurrentHeroUrl => null;
    public string? CurrentDetailPosterUrl => null;

    public MenuCurationPreviewVm Preview { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
        if (Input.CategoryId == Guid.Empty && CategoryOptions.Count > 0)
            Input.CategoryId = CategoryOptions[0].Id;

        Preview = BuildPreviewFromInput();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
        Preview = BuildPreviewFromInput();

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

        var entity = new MenuItem
        {
            Id = Guid.NewGuid(),
        };
        MenuItemProjections.ApplyFormToEntity(Input, entity);

        db.MenuItems.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (Input.HeroImage is { Length: > 0 })
            {
                var url = await imageStorage.SaveHeroAsync(Input.HeroImage, entity.Id, cancellationToken);
                if (url is not null)
                    entity.ImageUrl = url;
            }

            if (Input.DetailPosterImage is { Length: > 0 })
            {
                var url = await imageStorage.SavePosterAsync(Input.DetailPosterImage, entity.Id, cancellationToken);
                if (url is not null)
                    entity.DetailPosterImagePath = url;
            }

            if (entity.ImageUrl is not null || entity.DetailPosterImagePath is not null)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await imageStorage.DeleteHeroAsync(entity.Id, entity.ImageUrl, cancellationToken);
            await imageStorage.DeletePosterAsync(entity.Id, entity.DetailPosterImagePath, cancellationToken);
            db.MenuItems.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            ModelState.AddModelError(nameof(Input.HeroImage), ex.Message);
            Preview = BuildPreviewFromInput();
            return Page();
        }

        TempData["MenuCurationToast"] = "Đã thêm món vào thực đơn.";
        return RedirectToPage("/Admin/Menu/Index");
    }

    private MenuCurationPreviewVm BuildPreviewFromInput()
    {
        var catName = CategoryOptions.FirstOrDefault(c => c.Id == Input.CategoryId)?.Name
                      ?? CategoryOptions.FirstOrDefault()?.Name ?? "—";
        return new MenuCurationPreviewVm(
            string.IsNullOrWhiteSpace(Input.Name) ? "Tên món" : Input.Name.Trim(),
            NullIfWhite(Input.Subtitle),
            Input.Price,
            catName,
            Input.IsAvailable,
            null,
            MenuMediaResolver.TryResolveCardImageUrl(null, null, null, null, Input.Name, catName) ?? "");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        CategoryOptions = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new MenuCategorySelectOption(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    private static string? NullIfWhite(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}