using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.Services;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Menu;

public sealed class DrinkModel(IApplicationDbContext db, IMenuInventoryGate inventoryGate) : PageModel
{
    public IActionResult OnGet(Guid id, Guid? vt)
    {
        // The poster viewer is a client-side overlay; direct URL navigation redirects to the menu.
        var url = vt is Guid g
            ? $"/Menu/Index?openDrink={Uri.EscapeDataString(id.ToString("D"))}&vt={Uri.EscapeDataString(g.ToString("D"))}"
            : $"/Menu/Index?openDrink={Uri.EscapeDataString(id.ToString("D"))}";
        return Redirect(url);
    }

    /// <summary>JSON for client-side DrinkDetailRenderer.</summary>
    public async Task<IActionResult> OnGetDataAsync(Guid id, CancellationToken cancellationToken)
    {
        var vm = await BuildVmAsync(id, cancellationToken);
        if (vm is null)
            return NotFound();

        var pairings = await BakeryPairingService.GetSuggestionsForDrinkAsync(
            db,
            inventoryGate,
            id,
            vm.CategoryName,
            cancellationToken);

        return new JsonResult(MenuItemProjections.ToRendererDto(vm, pairings));
    }

    private async Task<MenuDrinkDetailVm?> BuildVmAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems
            .AsNoTracking()
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsArchived, cancellationToken);

        if (item is null)
            return null;

        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken);
        var heldByPantry = blocked.Contains(item.Id);
        string? serviceNote = null;
        if (heldByPantry)
            serviceNote = "This cup is resting while the pantry catches up.";
        else if (!item.IsAvailable)
            serviceNote = "This pour is resting for now.";

        var relatedRows = await db.MenuItems
            .AsNoTracking()
            .Where(i => i.CategoryId == item.CategoryId && i.Id != id && i.IsAvailable && !i.IsArchived && !blocked.Contains(i.Id))
            .OrderBy(i => i.Name)
            .Take(4)
            .Select(i => new { i.Id, i.Name, i.Price, i.Subtitle })
            .ToListAsync(cancellationToken);
        var related = relatedRows
            .Select(i => new RelatedDrinkVm(i.Id, i.Name, i.Price, i.Subtitle))
            .ToList();

        if (related.Count < 4)
        {
            var extraRows = await db.MenuItems
                .AsNoTracking()
                .Where(i => i.CategoryId != item.CategoryId && i.Id != id && i.IsAvailable && !i.IsArchived && !blocked.Contains(i.Id))
                .OrderBy(i => i.Name)
                .Take(4 - related.Count)
                .Select(i => new { i.Id, i.Name, i.Price, i.Subtitle })
                .ToListAsync(cancellationToken);
            related = related
                .Concat(extraRows.Select(i => new RelatedDrinkVm(i.Id, i.Name, i.Price, i.Subtitle)))
                .ToList();
        }

        return MenuItemProjections.ToEditorialDetail(item, related, serviceNote);
    }
}
