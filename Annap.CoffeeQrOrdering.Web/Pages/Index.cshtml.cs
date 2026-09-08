using System.Text.Json;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Pages;

public sealed class IndexModel(
    IApplicationDbContext db,
    IHostEnvironment env,
    IHttpContextAccessor httpContextAccessor,
    IOptions<DiagnosticsOptions> diagnostics,
    IOptions<GuestOperationalOptions> guestOperational)
    : PageModel
{
    public List<SignatureDrinkVm> SignatureDrinks { get; private set; } = [];
    public List<CategoryVm> Categories { get; private set; } = [];
    public List<CartCatalogRowVm> CartCatalog { get; private set; } = [];

    public bool IsDevelopment => env.IsDevelopment();

    /// <summary>LAN isolation: skip mood/sommelier/tray scripts; only menu fetch on home (<c>Diagnostics:SlimGuestBoot</c>).</summary>
    public bool SlimGuestBoot => (env.IsDevelopment() || diagnostics.Value.DeveloperOverlays) && diagnostics.Value.SlimGuestBoot;

    /// <summary>When true, seated guest homepage shows a primary link to the menu before optional experiences.</summary>
    public bool MenuFirstArrival => guestOperational.Value.MenuFirstArrival;

    /// <summary>When true, seated guest homepage uses calmer CSS motion for WebViews and low-end devices.</summary>
    public bool CalmArrivalAnimations => guestOperational.Value.CalmArrivalAnimations;

    /// <summary>Set when the guest arrives via table QR (<c>/table/T12</c> or <c>/t/annap-t12</c>).</summary>
    public Guid? VenueTableId { get; private set; }

    /// <summary>Display for hero and tray (falls back to table code).</summary>
    public string? TableGuestLabel { get; private set; }

    public string? PublicSlug { get; private set; }

    public bool HasSeatedTable => VenueTableId is not null;

    /// <summary>Guest scanned <c>/table</c> or <c>/t</c> but no active table matched.</summary>
    public bool QrScanInvalid { get; private set; }

    /// <summary><c>?vt=</c> was supplied but did not resolve to an active table.</summary>
    public bool TableHandoffInvalid { get; private set; }

    public GuestTableContextState TableContextState =>
        GuestTableContext.Resolve(HasSeatedTable, QrScanInvalid, TableHandoffInvalid);

    public async Task OnGetAsync(Guid? vt, CancellationToken cancellationToken)
    {
        VenueTable? seated = null;

        // Hand back from menu / drink pages (?vt=…) so home stays “seated” without re-scanning the table QR.
        if (vt is Guid handoffId)
        {
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == handoffId && v.IsActive, cancellationToken);
        }

        var tableDisplayCode =
            httpContextAccessor.HttpContext?.Items["QrTableDisplayCode"] as string
            ?? Request.Query["tc"].FirstOrDefault();
        var publicSlug =
            httpContextAccessor.HttpContext?.Items["QrPublicSlug"] as string
            ?? Request.Query["ts"].FirstOrDefault();

        if (seated is null && !string.IsNullOrWhiteSpace(publicSlug))
        {
            var slug = publicSlug.Trim().ToLowerInvariant();
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PublicSlug == slug && v.IsActive, cancellationToken);
        }
        else if (seated is null && !string.IsNullOrWhiteSpace(tableDisplayCode))
        {
            var code = tableDisplayCode.Trim();
            seated = await db.VenueTables.AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.VenueCode == "annap" && v.DisplayCode.ToLower() == code.ToLower() && v.IsActive,
                    cancellationToken);
        }

        if (seated is not null)
        {
            VenueTableId = seated.Id;
            PublicSlug = seated.PublicSlug;
            TableGuestLabel = string.IsNullOrWhiteSpace(seated.DisplayLabel) ? seated.DisplayCode : seated.DisplayLabel;
        }
        else
        {
            var qrAttempted =
                !string.IsNullOrWhiteSpace(httpContextAccessor.HttpContext?.Items["QrTableDisplayCode"] as string)
                || !string.IsNullOrWhiteSpace(httpContextAccessor.HttpContext?.Items["QrPublicSlug"] as string)
                || !string.IsNullOrWhiteSpace(publicSlug)
                || !string.IsNullOrWhiteSpace(tableDisplayCode);
            if (qrAttempted)
                QrScanInvalid = true;
            else if (vt is Guid)
                TableHandoffInvalid = true;
        }

        // When any signature slot rows exist, the group rail is CMS-owned — never fall back to MenuItem.IsSignature
        // (fallback previously showed "old" flagged drinks whenever the strict join returned zero rows).
        var hasSignatureRailCuration = await db.ExperienceSignatureSlots.AsNoTracking()
            .AnyAsync(cancellationToken);

        // Two-step load: slot curation + live MenuItem row guarantees canonical media (ImageUrl) is always read from menu_items,
        // avoiding any join/projection edge cases with navigation properties.
        var activeSlotRows = await db.ExperienceSignatureSlots.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.MenuItemId, s.EditorialKicker, s.EditorialBody })
            .ToListAsync(cancellationToken);

        if (activeSlotRows.Count > 0)
        {
            var slotIds = activeSlotRows.Select(s => s.MenuItemId).Distinct().ToList();
            var menuById = await db.MenuItems.AsNoTracking()
                .Where(m => slotIds.Contains(m.Id) && m.IsAvailable && !m.IsArchived)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Subtitle,
                    m.Description,
                    m.Price,
                    CategoryName = m.Category.Name,
                    m.ImageUrl,
                    m.DetailPosterImagePath
                })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            SignatureDrinks = [];
            foreach (var s in activeSlotRows)
            {
                if (!menuById.TryGetValue(s.MenuItemId, out var m))
                    continue;
                SignatureDrinks.Add(new SignatureDrinkVm(
                    m.Id,
                    m.Name,
                    s.EditorialKicker ?? m.Subtitle,
                    s.EditorialBody ?? m.Description,
                    m.Price,
                    m.CategoryName,
                    MenuMediaResolver.ResolveCardImageUrl(
                        null, null, m.ImageUrl, null, m.Name, m.CategoryName, m.DetailPosterImagePath)));
            }
        }
        else if (!hasSignatureRailCuration)
        {
            var sigQuery = db.MenuItems
                .AsNoTracking()
                .Where(x => x.IsAvailable && !x.IsArchived && x.IsSignature);

            if (!await sigQuery.AnyAsync(cancellationToken))
            {
                sigQuery = db.MenuItems.AsNoTracking().Where(x => x.IsAvailable && !x.IsArchived);
            }

            var sigRows = await sigQuery
                .OrderByDescending(x => x.IsSignature)
                .ThenByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Subtitle,
                    DescriptionLine = x.Description,
                    x.Price,
                    CategoryName = x.Category.Name,
                    x.ImageUrl,
                    x.DetailPosterImagePath
                })
            .ToListAsync(cancellationToken);
            SignatureDrinks = sigRows
                .Select(x => new SignatureDrinkVm(
                    x.Id,
                    x.Name,
                    x.Subtitle,
                    x.DescriptionLine,
                    x.Price,
                    x.CategoryName,
                    MenuMediaResolver.ResolveCardImageUrl(
                        null, null, x.ImageUrl, null, x.Name, x.CategoryName, x.DetailPosterImagePath)))
                .ToList();
        }
        else
        {
            SignatureDrinks = [];
        }

        Categories = await db.MenuCategories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new CategoryVm(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var catalogRows = await db.MenuItems
            .AsNoTracking()
            .Where(x => x.IsAvailable && !x.IsArchived)
            .OrderBy(x => x.DisplaySortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Price,
                CategoryName = x.Category.Name,
                x.ItemType
            })
            .ToListAsync(cancellationToken);

        CartCatalog = catalogRows
            .Select(x => new CartCatalogRowVm(
                x.Id,
                x.Name,
                x.Price,
                x.CategoryName,
                DrinkServingTemperature.Supports(x.CategoryName, x.Name, x.ItemType)))
            .ToList();

        // Seated arrival HTML carries live CMS payloads — discourage shared caches from serving a stale rail.
        if (VenueTableId is not null)
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    }
}

public sealed record SignatureDrinkVm(
    Guid Id,
    string Name,
    string? Subtitle,
    string? DescriptionLine,
    decimal Price,
    string CategoryName,
    string CardImageUrl);

public sealed record CategoryVm(Guid Id, string Name);

public sealed record CartCatalogRowVm(
    Guid Id,
    string Name,
    decimal Price,
    string CategoryName,
    bool SupportsHotIced);
