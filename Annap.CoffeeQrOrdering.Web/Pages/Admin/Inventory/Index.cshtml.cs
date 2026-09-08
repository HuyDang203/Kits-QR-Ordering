using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Inventory;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db, IMenuInventoryGate inventoryGate) : PageModel
{
    public IReadOnlyList<IngredientRowVm> Ingredients { get; private set; } = [];

    public IReadOnlyList<string> CupsHeldByPantry { get; private set; } = [];

    public IReadOnlyList<string> CupsPausedOnLine { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken);
        var ingRows = await db.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToListAsync(cancellationToken);
        Ingredients = ingRows.Select(i => new IngredientRowVm(
            i.Id,
            i.Name,
            i.Unit,
            i.CurrentStock,
            i.LowStockThreshold,
            i.IsActive,
            i.IsActive && i.CurrentStock <= i.LowStockThreshold)).ToList();

        var heldNames = await db.MenuItems.AsNoTracking()
            .Where(m => blocked.Contains(m.Id))
            .OrderBy(m => m.Name)
            .Select(m => m.Name)
            .ToListAsync(cancellationToken);
        CupsHeldByPantry = heldNames;

        var paused = await db.MenuItems.AsNoTracking()
            .Where(m => !m.IsAvailable && !m.IsArchived)
            .OrderBy(m => m.Name)
            .Select(m => m.Name)
            .ToListAsync(cancellationToken);
        CupsPausedOnLine = paused;
    }

    public async Task<IActionResult> OnPostAdjustStockAsync(Guid id, decimal delta, CancellationToken cancellationToken)
    {
        var row = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (row is null)
            return RedirectToPage();

        row.CurrentStock = Math.Max(0, row.CurrentStock + delta);
        await db.SaveChangesAsync(cancellationToken);
        TempData["InventoryToast"] = "Pantry count updated — the room will feel it quietly.";
        return RedirectToPage();
    }
}

public sealed record IngredientRowVm(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentStock,
    decimal LowStockThreshold,
    bool IsActive,
    bool IsLow);
