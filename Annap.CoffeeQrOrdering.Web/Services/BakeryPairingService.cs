using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Projections;
using Annap.CoffeeQrOrdering.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Loads bakery (Bánh) suggestions for drink detail pairings.</summary>
public static class BakeryPairingService
{
    /// <summary>
    /// Canonical Kits bakery category. After Phase 7, the database contains exactly one
    /// category row matching this name. Legacy variants (<c>Bánh</c>, <c>Banh</c>,
    /// <c>BANH</c>) are normalized by <c>MergeBakeryCategoryVariants</c> before this
    /// service ever runs.
    /// </summary>
    public static string BakeryCategoryName => KitsMenuCatalog.Categories.Banh;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="categoryName"/> matches the canonical
    /// Kits bakery category. Comparison is case-insensitive and diacritic-insensitive
    /// because both <c>BÁNH</c> and <c>Banh</c> map to the same logical category once
    /// the migration has run.
    /// </summary>
    public static bool IsBakeryCategory(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;
        var t = categoryName.Trim();
        return string.Equals(t, BakeryCategoryName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(t, "Banh", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task EnsureBakeryCategoryAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var canonicalName = BakeryCategoryName;
        var exists = await db.MenuCategories
            .AnyAsync(c => c.Name == canonicalName, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        var maxSort = await db.MenuCategories
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        db.MenuCategories.Add(new Domain.Entities.MenuCategory
        {
            Name = canonicalName,
            SortOrder = (maxSort ?? 7) + 1
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<PairingSuggestionDto>> GetSuggestionsForDrinkAsync(
        IApplicationDbContext db,
        IMenuInventoryGate inventoryGate,
        IPricingService pricing,
        Guid drinkId,
        string? drinkCategoryName,
        CancellationToken cancellationToken = default)
    {
        if (IsBakeryCategory(drinkCategoryName))
            return Array.Empty<PairingSuggestionDto>();

        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);

        var canonicalName = BakeryCategoryName;
        var bakeryItems = await db.MenuItems
            .AsNoTracking()
            .Include(i => i.Category)
            .Where(i =>
                i.Category != null
                && (i.Category.Name == canonicalName || i.Category.Name == "Banh")
                && i.IsAvailable
                && !i.IsArchived
                && !blocked.Contains(i.Id))
            .OrderBy(i => i.DisplaySortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.ImageUrl, i.DetailPosterImagePath, i.Price, CategoryName = i.Category!.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bakeryItems.Count == 0)
            return Array.Empty<PairingSuggestionDto>();

        var offset = Math.Abs(drinkId.GetHashCode()) % bakeryItems.Count;
        var rotated = bakeryItems.Skip(offset).Concat(bakeryItems.Take(offset)).Take(3).ToList();

        var dtos = new List<PairingSuggestionDto>(rotated.Count);
        foreach (var i in rotated)
        {
            var card = MenuMediaResolver.TryResolveCardImageUrl(
                null, null, i.ImageUrl, null, i.Name, i.CategoryName, i.DetailPosterImagePath) ?? "";

            // Route effective price through the same IPricingService used by
            // /api/menu and POST /api/orders so the cart never sees a different
            // number than the receipt the server will issue.
            var effective = await pricing
                .GetEffectivePriceAsync(i.Price, cancellationToken)
                .ConfigureAwait(false);
            var hasDiscount = effective != i.Price && i.Price > 0;

            dtos.Add(new PairingSuggestionDto
            {
                Id = i.Id,
                Name = i.Name,
                Image = card,
                Price = effective,
                OriginalPrice = i.Price,
                PriceDisplay = VndMoneyFormatter.Format(effective),
                OriginalPriceDisplay = hasDiscount ? VndMoneyFormatter.Format(i.Price) : null
            });
        }
        return dtos;
    }
}
