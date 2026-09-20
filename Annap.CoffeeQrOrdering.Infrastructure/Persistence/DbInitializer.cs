using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

/// <summary>
/// One-shot legacy menu/category helpers used during the Annap → Kits migration.
///
/// <para>
/// <b>PRODUCTION SAFETY (Phase 9):</b> All methods in this class are intentionally
/// non-destructive against any database that may already contain historical
/// <c>Order</c>/<c>OrderItem</c> rows. Methods that previously issued
/// <c>DELETE FROM menu_items</c> / <c>DELETE FROM menu_categories</c> have been
/// converted into safe no-ops. They return <c>0</c> and log a warning so any
/// caller (current or future) cannot accidentally break the production
/// foreign-key graph:
/// </para>
/// <list type="bullet">
///   <item><see cref="TryRemoveLegacyDemoMenuIfUnusedAsync"/> — no-op</item>
///   <item><see cref="TryRemoveLegacyCategoriesIfEmptyAsync"/> — no-op</item>
///   <item><see cref="TryReconcileKitsCatalogAsync"/> — no-op</item>
/// </list>
/// <para>
/// Legacy category and menu row reconciliation is now performed by
/// <c>Annap.CoffeeQrOrdering.Web.Services.KitsMenuBootstrap</c>, which heals
/// rows in place (renaming legacy categories and upserting missing menu items)
/// without ever issuing a <c>DELETE</c> against <c>menu_items</c> or
/// <c>menu_categories</c>. Historical <c>OrderItem</c> rows therefore remain
/// valid against their <c>MenuItemId</c> foreign keys.
/// </para>
/// </summary>
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
    /// <b>No-op for production safety.</b> Previously deleted legacy prototype
    /// <c>MenuItem</c> rows that were not referenced by any <c>OrderItem</c>;
    /// in practice that delete is unsafe against a live production database
    /// (e.g. <c>OrderItem</c> rows can be inserted concurrently, or
    /// historical "legacy" items may still be required for audit trails).
    /// Legacy menu reconciliation is now handled additively by
    /// <c>KitsMenuBootstrap.EnsureKitsMenuAsync</c>: existing rows are left
    /// intact, missing canonical rows are inserted, and renamed categories are
    /// updated in place. Kept as a documented no-op so any historical caller
    /// remains a safe no-op rather than an accidental destructive call.
    /// </summary>
    public static async Task TryRemoveLegacyDemoMenuIfUnusedAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        // Production safety: this method MUST NOT issue DELETE against menu_items.
        // Historical OrderItem rows reference MenuItem rows by FK RESTRICT, and
        // any delete attempt against a referenced row throws DbUpdateException.
        await Task.CompletedTask.ConfigureAwait(false);
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
    /// <b>No-op for production safety.</b> Previously removed legacy
    /// <c>MenuCategory</c> rows that had no non-archived items attached. Even
    /// for "empty" legacy categories, the bootstrap must not delete the row
    /// because:
    /// <list type="bullet">
    ///   <item>Archived items may still be referenced by historical <c>OrderItem</c> rows.</item>
    ///   <item>Admin-side tooling may rely on the legacy row existing.</item>
    ///   <item>Deleting a category cascades to its archived items via the
    ///         configured <c>MenuItem.CategoryId</c> FK CASCADE, which would
    ///         also violate <c>OrderItem.MenuItemId</c> FK RESTRICT.</item>
    /// </list>
    /// Legacy categories that need to disappear from the active catalog are
    /// already hidden because every consumer query filters by
    /// <c>IsAvailable &amp;&amp; !IsArchived</c> on items. No row needs to be
    /// physically removed to achieve the desired UX.
    /// </summary>
    public static async Task<int> TryRemoveLegacyCategoriesIfEmptyAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// <b>No-op for production safety.</b> Previously reconciled the active
    /// catalog by:
    /// <list type="bullet">
    ///   <item>hard-deleting legacy <c>MenuItem</c> rows with no order history,</item>
    ///   <item>reassigning archived legacy items into the <c>CAFE</c> category,</item>
    ///   <item>hard-deleting non-canonical <c>MenuCategory</c> rows.</item>
    /// </list>
    /// Those operations violate the strict data safety rules for an existing
    /// production database (see class-level docstring): deleting a
    /// <c>MenuItem</c> referenced by a historical <c>OrderItem</c> raises
    /// <c>PostgresException 23503</c> ("violates RESTRICT setting of foreign
    /// key constraint FK_order_items_menu_items_MenuItemId"), and deleting a
    /// <c>MenuCategory</c> cascades to its remaining items, which in turn
    /// collide with the same FK. The reconciliation is therefore performed
    /// purely additively by <c>KitsMenuBootstrap.EnsureKitsMenuAsync</c>:
    /// <list type="bullet">
    ///   <item>canonical category names are normalized in place,</item>
    ///   <item>canonical menu items are upserted by <c>CatalogKey</c>,</item>
    ///   <item>historical items not in the current canonical seed are left
    ///         untouched so their <c>OrderItem</c> foreign keys stay valid.</item>
    /// </list>
    /// Kept as a documented no-op so historical callers stay safe; returns an
    /// empty result and logs a warning if a logger is supplied.
    /// </summary>
    public static async Task<KitsCatalogReconciliationResult> TryReconcileKitsCatalogAsync(
        IApplicationDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        logger?.LogWarning(
            "DbInitializer.TryReconcileKitsCatalogAsync is a no-op (production safety). " +
            "Use KitsMenuBootstrap.EnsureKitsMenuAsync for additive reconciliation.");
        await Task.CompletedTask.ConfigureAwait(false);
        return new KitsCatalogReconciliationResult();
    }

    public sealed class KitsCatalogReconciliationResult
    {
        public int ArchivedCount { get; set; }
        public int HardDeletedCount { get; set; }
        public int ReassignedCount { get; set; }
        public int CategoriesDeleted { get; set; }
    }
}
