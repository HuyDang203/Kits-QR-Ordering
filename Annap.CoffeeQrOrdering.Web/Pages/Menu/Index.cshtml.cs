using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;
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
    }
}
