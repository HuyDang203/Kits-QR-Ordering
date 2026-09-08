using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly string[] LegacyDemoMenuNames =
    [
        "Single Origin Espresso",
        "Cappuccino",
        "V60 Pour-Over",
        "Cardamom Latte",
        "Affogato",
        "Dura Farm",
        "Ginger's Singer"
    ];

    private static readonly string[] LegacyDemoIngredientNames =
    [
        "House espresso blend",
        "Whole milk"
    ];

    /// <summary>Seeds QR-identified service tables (idempotent).</summary>
    public static async Task EnsureVenueTablesAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.VenueTables.AnyAsync(cancellationToken))
            return;

        const string venue = "annap";
        var tables = new List<VenueTable>();
        for (var i = 1; i <= 36; i++)
        {
            var code = $"T{i:00}";
            tables.Add(new VenueTable
            {
                VenueCode = venue,
                DisplayCode = code,
                PublicSlug = $"{venue}-{code.ToLowerInvariant()}",
                DisplayLabel = $"Table {i}",
                IsActive = true
            });
        }

        db.VenueTables.AddRange(tables);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes legacy prototype menu rows when they are not referenced by any submitted order line.
    /// Safe for production: historical orders keep their menu rows until archived elsewhere.
    /// </summary>
    public static async Task TryRemoveLegacyDemoMenuIfUnusedAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var legacySet = LegacyDemoMenuNames.ToHashSet(StringComparer.Ordinal);
        var candidates = await db.MenuItems
            .Where(m => legacySet.Contains(m.Name))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var used = await db.OrderItems
            .Where(o => candidates.Contains(o.MenuItemId))
            .Select(o => o.MenuItemId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var usedSet = used.ToHashSet();

        foreach (var id in candidates)
        {
            if (usedSet.Contains(id))
                continue;
            var row = await db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (row is not null)
                db.MenuItems.Remove(row);
        }

        await db.SaveChangesAsync(cancellationToken);

        var emptyLegacyCategories = await db.MenuCategories
            .Where(c =>
                c.Name == "Espresso Bar"
                || c.Name == "Filter & Pour-Over"
                || c.Name == "Signature")
            .Where(c => !db.MenuItems.Any(m => m.CategoryId == c.Id))
            .ToListAsync(cancellationToken);

        if (emptyLegacyCategories.Count > 0)
        {
            db.MenuCategories.RemoveRange(emptyLegacyCategories);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var ingName in LegacyDemoIngredientNames)
        {
            var ing = await db.Ingredients.FirstOrDefaultAsync(i => i.Name == ingName, cancellationToken);
            if (ing is null)
                continue;
            var linked = await db.MenuItemIngredients.AnyAsync(m => m.IngredientId == ing.Id, cancellationToken);
            if (!linked)
                db.Ingredients.Remove(ing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Annap legacy category names that must not remain in the active Kits catalog.
    /// The canonical Kits categories are <c>CAFE</c>, <c>TRÀ</c>, <c>BÁNH</c>, <c>COMBO</c>;
    /// anything else must be removed (or archived if a non-empty live menu references it).
    /// </summary>
    internal static readonly string[] LegacyCategoryNames =
    [
        "Espresso Bar",
        "Filter & Pour-Over",
        "Signature",
        "Specialty Coffee",
        "Espresso",
        "Coffee",
        "Tea",
        "Smoothie",
        "Juice",
        "Cold Brew",
        "Vietnamese Coffee",
    ];

    /// <summary>
    /// Archives any remaining Annap-prototype <c>MenuItem</c> row that is still
    /// referenced by historical <c>OrderItem</c> rows (so it cannot be hard-deleted
    /// without breaking the FK). Once archived, the row no longer appears in any
    /// guest or admin catalog query (both filter by <c>IsAvailable &amp;&amp; !IsArchived</c>).
    /// Historical order tickets remain readable because <c>OrderItem.MenuItemName</c>
    /// was already snapshotted at order submit time by the
    /// <c>Phase2_OrderItemSnapshotAndSafety</c> migration.
    /// Idempotent: re-running produces no further change once the rows are archived.
    /// </summary>
    public static async Task<int> TryArchiveLegacyDemoMenuIfReferencedAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var legacySet = LegacyDemoMenuNames.ToHashSet(StringComparer.Ordinal);

        var referencedLegacyIds = await db.OrderItems
            .Where(o => db.MenuItems.Any(m => m.Id == o.MenuItemId && legacySet.Contains(m.Name)))
            .Select(o => o.MenuItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (referencedLegacyIds.Count == 0)
            return 0;

        var archived = 0;
        foreach (var id in referencedLegacyIds)
        {
            var row = await db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (row is null)
                continue;
            if (row.IsArchived && !row.IsAvailable)
                continue;
            row.IsArchived = true;
            row.IsAvailable = false;
            archived++;
        }

        if (archived > 0)
            await db.SaveChangesAsync(cancellationToken);

        return archived;
    }

    /// <summary>
    /// Removes any Annap-prototype <c>MenuCategory</c> row that is no longer
    /// referenced by any non-archived <c>MenuItem</c>. Safe: it can only run
    /// against categories whose active item set is empty (archived items do
    /// not count because they are excluded from the catalog).
    /// Idempotent: once empty legacy categories are removed, this is a no-op.
    /// </summary>
    public static async Task<int> TryRemoveLegacyCategoriesIfEmptyAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var legacyCatNames = LegacyCategoryNames.ToHashSet(StringComparer.Ordinal);
        var candidates = await db.MenuCategories
            .Where(c => legacyCatNames.Contains(c.Name))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return 0;

        var empty = new List<MenuCategory>();
        foreach (var c in candidates)
        {
            var hasLiveItem = await db.MenuItems
                .AnyAsync(m => m.CategoryId == c.Id && !m.IsArchived, cancellationToken);
            if (!hasLiveItem)
                empty.Add(c);
        }

        if (empty.Count == 0)
            return 0;

        db.MenuCategories.RemoveRange(empty);
        await db.SaveChangesAsync(cancellationToken);
        return empty.Count;
    }

    /// <summary>
    /// Full Kits catalog reconciliation: archives all legacy menu items (non-<c>kits-</c>
    /// catalog key) that are referenced by historical orders, hard-deletes those that are not,
    /// reassigns archived items in non-canonical categories to the canonical CAFE category
    /// (to satisfy FK RESTRICT before category deletion), then removes the 8 legacy
    /// categories that are not part of the 4-category Kits catalog.
    ///
    /// The 4 canonical Kits categories (CAFE, TRÀ, BÁNH, COMBO) are preserved.
    /// Historical order tickets remain readable because <c>OrderItem.MenuItemName</c>
    /// was snapshotted at submit time.
    ///
    /// This method is safe to re-run: once 46 legacy items are processed it becomes a no-op.
    /// </summary>
    public static async Task<KitsCatalogReconciliationResult> TryReconcileKitsCatalogAsync(
        IApplicationDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var result = new KitsCatalogReconciliationResult();

        // ── 1. Identify the canonical CAFE category ID ──────────────────────────────
        var cafeCategory = await db.MenuCategories
            .FirstOrDefaultAsync(c => c.Name == "CAFE", cancellationToken)
            .ConfigureAwait(false);
        if (cafeCategory == null)
        {
            logger?.LogWarning(
                "Kits catalog reconciliation skipped: canonical 'CAFE' category not found.");
            return result;
        }

        // ── 2. Identify all non-canonical category names ───────────────────────────
        var canonicalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CAFE", "TRÀ", "BÁNH", "COMBO"
        };
        var nonCanonicalCats = await db.MenuCategories
            .Where(c => !canonicalNames.Contains(c.Name))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nonCanonicalCatIds = nonCanonicalCats.Select(c => c.Id).ToHashSet();

        // ── 3. Archive all legacy items (non-kits prefix) that have order history ──
        //       These cannot be hard-deleted due to FK RESTRICT on order_items.
        var legacyItems = await db.MenuItems
            .Where(m => m.CatalogKey == null || !m.CatalogKey.StartsWith("kits-"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (legacyItems.Count == 0)
        {
            logger?.LogInformation(
                "Kits catalog reconciliation: no legacy menu items found — nothing to reconcile.");
            return result;
        }

        var legacyIds = legacyItems.Select(m => m.Id).ToList();

        // Check which legacy items have order history
        var usedIds = await db.OrderItems
            .Where(o => legacyIds.Contains(o.MenuItemId))
            .Select(o => o.MenuItemId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var usedSet = usedIds.ToHashSet();

        var toArchive = legacyItems.Where(m => usedSet.Contains(m.Id) && !m.IsArchived).ToList();
        var toHardDelete = legacyItems.Where(m => !usedSet.Contains(m.Id)).ToList();

        foreach (var item in toArchive)
        {
            item.IsArchived = true;
            item.IsAvailable = false;
            result.ArchivedCount++;
        }
        if (toArchive.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation(
                "Kits catalog reconciliation: archived {ArchivedCount} legacy item(s) with order history.",
                toArchive.Count);
        }

        // ── 4. Reassign archived items in non-canonical categories to CAFE ─────────
        //       This prevents FK RESTRICT failures when we delete non-canonical categories.
        foreach (var item in legacyItems.Where(m => m.IsArchived && nonCanonicalCatIds.Contains(m.CategoryId)))
        {
            if (item.CategoryId != cafeCategory.Id)
            {
                item.CategoryId = cafeCategory.Id;
                result.ReassignedCount++;
            }
        }
        if (result.ReassignedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation(
                "Kits catalog reconciliation: reassigned {ReassignedCount} archived item(s) to 'CAFE' category.",
                result.ReassignedCount);
        }

        // ── 5. Hard-delete legacy items not referenced by orders ───────────────────
        foreach (var item in toHardDelete)
        {
            db.MenuItems.Remove(item);
            result.HardDeletedCount++;
        }
        if (toHardDelete.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation(
                "Kits catalog reconciliation: hard-deleted {HardDeletedCount} legacy item(s) with no order history.",
                toHardDelete.Count);
        }

        // ── 6. Delete non-canonical categories (all are now empty of non-archived items) ──
        if (nonCanonicalCats.Count > 0)
        {
            var toDelete = await db.MenuCategories
                .Where(c => nonCanonicalCatIds.Contains(c.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            db.MenuCategories.RemoveRange(toDelete);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            result.CategoriesDeleted = toDelete.Count;
            logger?.LogInformation(
                "Kits catalog reconciliation: deleted {CategoriesDeleted} legacy category name(s): {Names}.",
                toDelete.Count,
                string.Join(", ", toDelete.Select(c => c.Name)));
        }

        return result;
    }

    public sealed class KitsCatalogReconciliationResult
    {
        public int ArchivedCount { get; set; }
        public int HardDeletedCount { get; set; }
        public int ReassignedCount { get; set; }
        public int CategoriesDeleted { get; set; }
    }
}
