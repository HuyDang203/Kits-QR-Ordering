using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Menu;

public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public List<MenuCategory> Categories { get; private set; } = [];

    /// <summary>Optional table handoff from home (<c>?vt=</c> or session on client).</summary>
    public Guid? VenueTableId { get; private set; }

    public string? TableGuestLabel { get; private set; }

    public bool HasSeatedTable => VenueTableId is not null;

    public bool TableHandoffInvalid { get; private set; }

    /// <summary>
    /// Server-resolved detail poster URL for every visible menu item, keyed by item id.
    /// Populated from the same <see cref="MenuMediaResolver.TryResolveDetailPosterUrl"/> call
    /// that powers the detail API so the browser can preload the image at pointerdown
    /// without an extra round-trip or Cloudinary URL derivation in JavaScript.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> DetailImageByItemId { get; private set; } =
        new Dictionary<Guid, string>();

    public GuestTableContextState TableContextState =>
        GuestTableContext.Resolve(HasSeatedTable, false, TableHandoffInvalid);

    public async Task OnGetAsync(Guid? vt, CancellationToken cancellationToken)
    {
        if (vt is Guid g)
        {
            var t = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == g && v.IsActive, cancellationToken);
            if (t is not null)
            {
                VenueTableId = t.Id;
                TableGuestLabel = string.IsNullOrWhiteSpace(t.DisplayLabel) ? t.DisplayCode : t.DisplayLabel;
            }
            else
            {
                TableHandoffInvalid = true;
            }
        }

        // Filter the catalogue at the presentation layer to only include canonical
        // categories that contain at least one available, non-archived menu item.
        // This is a safety boundary: the canonical Kits catalog (CAFE / TRÀ / BÁNH / COMBO)
        // is established by the bootstrap reconciliation, but if a legacy row slips
        // through (e.g. an admin manual insert or a concurrent bootstrap), it cannot
        // render in the guest UI.
        Categories = await db.MenuCategories
            .AsNoTracking()
            .Include(c => c.Items.Where(i => i.IsAvailable && !i.IsArchived))
            .OrderBy(c => c.SortOrder)
            .Where(c => c.Items.Any(i => i.IsAvailable && !i.IsArchived))
            .ToListAsync(cancellationToken);

        foreach (var cat in Categories)
        {
            cat.Items = cat.Items.OrderBy(i => i.DisplaySortOrder).ThenBy(i => i.Name).ToList();
        }

        // Pre-resolve each item's detail poster URL using the exact same call the
        // detail API performs. No additional DB query: MenuItem.DetailPosterImagePath
        // and MenuItem.ImageUrl are already in memory from the Categories include above.
        var byItem = new Dictionary<Guid, string>(capacity: 64);
        foreach (var cat in Categories)
        {
            foreach (var item in cat.Items)
            {
                byItem[item.Id] = MenuMediaResolver.TryResolveDetailPosterUrl(
                    item.DetailPosterImagePath,
                    item.ImageUrl,
                    item.Name,
                    cat.Name) ?? "";
            }
        }
        DetailImageByItemId = byItem;
    }
}
