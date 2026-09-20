using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// First-install / heal-missing bootstrap for the canonical KitsQR menu.
///
/// Creates the four required categories (CAFE / TRÀ / BÁNH / COMBO) and seeds the
/// 29 expected menu items using stable <c>kits-*</c> catalog keys. After a row exists,
/// the database is the source of truth — admin edits and the inventory pipeline take
/// over. The bootstrap is fully idempotent: rows are inserted only when the catalog key
/// is absent, and categories are aligned (not duplicated) on every run.
///
/// This class intentionally replaces the legacy Annap-specialty bootstrap. It does not
/// touch ordering, payment, signalr, staff, shift, QR/table, or inventory deduction logic.
/// Recipe lines for inventory are NOT populated here — that is an explicit later step.
/// </summary>
public static class KitsMenuBootstrap
{
    /// <summary>Ensures categories exist and the canonical 29 menu items are seeded.</summary>
    public static async Task<int> EnsureKitsMenuAsync(
        IApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoriesAsync(db, logger, cancellationToken).ConfigureAwait(false);

        var inserted = 0;
        foreach (var seed in KitsSeeds.Items)
        {
            inserted += await UpsertAsync(db, seed, cancellationToken).ConfigureAwait(false);
        }

        if (inserted > 0)
        {
            logger.LogInformation(
                "Kits menu bootstrap inserted {Count} new menu item(s) across {Categories} categories.",
                inserted,
                KitsMenuCatalog.Categories.Cafe + ", " + KitsMenuCatalog.Categories.Tra +
                ", " + KitsMenuCatalog.Categories.Banh + ", " + KitsMenuCatalog.Categories.Combo);
        }
        else
        {
            logger.LogInformation(
                "Kits menu bootstrap: database already contains the canonical 29 items — no inserts.");
        }

        return inserted;
    }

    /// <summary>
    /// Legacy bakery category variants that may still exist on legacy databases.
    /// PostgreSQL's default binary collation treats each of these as a distinct
    /// string from the canonical <see cref="KitsMenuCatalog.Categories.Banh"/> (<c>BÁNH</c>),
    /// so the bootstrap explicitly looks them up to avoid crashes. After Phase 7's
    /// <c>MergeBakeryCategoryVariants</c> migration runs, only <c>BÁNH</c> remains.
    /// </summary>
    internal static readonly string[] LegacyBakeryCategoryVariants = ["Bánh", "Banh", "BANH"];

    /// <summary>Ensures the five required categories exist (no duplicates, stable sort order).</summary>
    public static async Task EnsureCategoriesAsync(
        IApplicationDbContext db,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.MenuCategories
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byName = existing.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        // Idempotent category upserts with explicit sort order.
        // If the database still holds a row with the legacy name (e.g. "TRÀ" or "BÁNH"),
        // UpsertCategoryAsync will rename it to the canonical display value below.
        await UpsertCategoryAsync(db, byName, KitsMenuCatalog.Categories.Cafe, KitsMenuCatalog.SortCafe, cancellationToken);
        await UpsertCategoryAsync(db, byName, KitsMenuCatalog.Categories.Tra, KitsMenuCatalog.SortTra, cancellationToken);
        await UpsertBakeryCategoryAsync(db, byName, cancellationToken, logger).ConfigureAwait(false);
        await UpsertCategoryAsync(db, byName, KitsMenuCatalog.Categories.Combo, KitsMenuCatalog.SortCombo, cancellationToken);
        await UpsertCategoryAsync(db, byName, KitsMenuCatalog.Categories.Smoothie, KitsMenuCatalog.SortSmoothie, cancellationToken);

        if (changed)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Special-case upsert for the canonical <c>BÁNH</c> bakery category. If a legacy
    /// variant (e.g. <c>Bánh</c>, <c>Banh</c>, <c>BANH</c>) exists instead, heal the row
    /// in place to the canonical spelling + sort order so the foreign-key graph stays
    /// intact for any MenuItems already attached to it. This is safe to run before
    /// the <c>MergeBakeryCategoryVariants</c> migration on legacy databases and a no-op
    /// once the canonical row exists.
    /// </summary>
    private static async Task UpsertBakeryCategoryAsync(
        IApplicationDbContext db,
        Dictionary<string, MenuCategory> byName,
        CancellationToken cancellationToken,
        ILogger? logger)
    {
        var canonical = KitsMenuCatalog.Categories.Banh;
        var sortOrder = KitsMenuCatalog.SortBanh;

        if (byName.TryGetValue(canonical, out var canonicalRow))
        {
            if (canonicalRow.SortOrder != sortOrder)
            {
                canonicalRow.SortOrder = sortOrder;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        // Canonical row missing — try to heal a legacy variant in place.
        foreach (var variant in LegacyBakeryCategoryVariants)
        {
            if (byName.TryGetValue(variant, out var legacyRow))
            {
                legacyRow.Name = canonical;
                legacyRow.SortOrder = sortOrder;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                byName[canonical] = legacyRow;
                byName.Remove(variant);
                logger?.LogWarning(
                    "Kits menu bootstrap: legacy bakery category '{Legacy}' normalized in place to canonical '{Canonical}' (SortOrder={SortOrder}).",
                    variant,
                    canonical,
                    sortOrder);
                return;
            }
        }

        // No variant exists — create the canonical row.
        db.MenuCategories.Add(new MenuCategory
        {
            Name = canonical,
            SortOrder = sortOrder
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        byName[canonical] = await db.MenuCategories
            .FirstAsync(c => c.Name == canonical, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Idempotent category upsert. Looks up the row by canonical name first; if not found,
    /// checks legacy names (e.g. <c>TRÀ</c> or <c>BÁNH</c>) and heals the row to the
    /// canonical display value + sort order. Adds a new row only when no legacy variant exists.
    /// </summary>
    private static async Task UpsertCategoryAsync(
        IApplicationDbContext db,
        Dictionary<string, MenuCategory> byName,
        string name,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        if (byName.TryGetValue(name, out var category))
        {
            if (category.SortOrder != sortOrder)
            {
                category.SortOrder = sortOrder;
            }
            return;
        }

        // Heal legacy variant in place if a row with one of the legacy names exists.
        var legacyNames = LegacyCategoryDisplayAliases.TryGetValue(name, out var aliases)
            ? aliases
            : Array.Empty<string>();
        foreach (var legacy in legacyNames)
        {
            if (byName.TryGetValue(legacy, out var legacyRow))
            {
                legacyRow.Name = name;
                legacyRow.SortOrder = sortOrder;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                byName[name] = legacyRow;
                byName.Remove(legacy);
                return;
            }
        }

        db.MenuCategories.Add(new MenuCategory
        {
            Name = name,
            SortOrder = sortOrder
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        byName[name] = await db.MenuCategories
            .FirstAsync(c => c.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Legacy display aliases keyed by canonical category name. Used by the bootstrap to heal
    /// legacy rows in place so the FK graph stays intact for any <c>MenuItem</c> rows already
    /// attached. This is a one-way migration — running the bootstrap converges the database
    /// to the canonical names without creating duplicates.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> LegacyCategoryDisplayAliases =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Tea & Juice"] = ["TRÀ", "Trà", "Tra"],
            ["Cake & Ice Cream"] = ["BÁNH", "Bánh", "BANH", "Banh"],
        };

    private static async Task<int> UpsertAsync(
        IApplicationDbContext db,
        KitsMenuSeed seed,
        CancellationToken cancellationToken)
    {
        var categoryId = await ResolveCategoryIdAsync(db, seed.CategoryName, cancellationToken)
            .ConfigureAwait(false);

        var existing = await db.MenuItems
            .FirstOrDefaultAsync(m => m.CatalogKey == seed.CatalogKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Heal drift only — never overwrite admin edits to name/price/availability.
            if (existing.CategoryId != categoryId)
                existing.CategoryId = categoryId;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        db.MenuItems.Add(new MenuItem
        {
            CategoryId = categoryId,
            Name = seed.Name,
            CatalogKey = seed.CatalogKey,
            ItemType = seed.ItemType,
            Subtitle = seed.Subtitle,
            Description = seed.Description,
            Price = seed.Price,
            DisplaySortOrder = seed.DisplaySortOrder,
            IsAvailable = true,
            IsArchived = false,
            IsFeatured = seed.IsFeatured,
            IsSignature = seed.IsSignature,
            ImageUrl = null,
            DetailPosterImagePath = null,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <summary>
    /// Resolves a category Id by exact name. For the bakery category only, falls back to
    /// legacy variant spellings so legacy rows are reused rather than crashing the
    /// <c>FirstAsync</c> with <c>Sequence contains no elements</c>.
    /// </summary>
    private static async Task<Guid> ResolveCategoryIdAsync(
        IApplicationDbContext db,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var id = await db.MenuCategories
            .Where(c => c.Name == categoryName)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (id != Guid.Empty)
            return id;

        if (string.Equals(categoryName, KitsMenuCatalog.Categories.Banh, StringComparison.Ordinal))
        {
            foreach (var variant in LegacyBakeryCategoryVariants)
            {
                id = await db.MenuCategories
                    .Where(c => c.Name == variant)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (id != Guid.Empty)
                    return id;
            }
        }

        throw new InvalidOperationException(
            $"Kits menu bootstrap: required category '{categoryName}' is missing. " +
            "Apply the MergeBakeryCategoryVariants migration or run KitsMenuBootstrap.EnsureCategoriesAsync first.");
    }

    /// <summary>Single seed descriptor (private record — never exposed).</summary>
    public sealed record KitsMenuSeed(
        string CatalogKey,
        string CategoryName,
        string Name,
        string? ItemType,
        string? Subtitle,
        string? Description,
        decimal Price,
        int DisplaySortOrder,
        bool IsFeatured = false,
        bool IsSignature = false);

    /// <summary>The 29 canonical Kits items.</summary>
    private static class KitsSeeds
    {
        public static readonly KitsMenuSeed[] Items =
        [
            // CAFE — Cà phê phin (4)
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-ca-phe-den-da",    KitsMenuCatalog.Categories.Cafe, "Cà phê đen đá",    KitsMenuCatalog.ItemTypes.Iced,  "Cà phê phin truyền thống, đá",                   null, 30_000m, 1),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-ca-phe-sua-da",   KitsMenuCatalog.Categories.Cafe, "Cà phê sữa đá",   KitsMenuCatalog.ItemTypes.Iced,  "Cà phê phin, sữa đặc, đá",                       null, 35_000m, 2, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-bac-xiu",         KitsMenuCatalog.Categories.Cafe, "Bạc xỉu",         KitsMenuCatalog.ItemTypes.Iced,  "Cà phê phin, sữa tươi, đá",                      null, 38_000m, 3),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-ca-phe-muoi",     KitsMenuCatalog.Categories.Cafe, "Cà phê muối",     KitsMenuCatalog.ItemTypes.Iced,  "Cà phê phin, kem muối, đá",                       null, 40_000m, 4),

            // CAFE — Cà phê máy (6)
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-espresso",        KitsMenuCatalog.Categories.Cafe, "Espresso",         KitsMenuCatalog.ItemTypes.Hot,   "Espresso đơn, đậm đà",                            null, 35_000m, 5),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-americano",      KitsMenuCatalog.Categories.Cafe, "Americano",        KitsMenuCatalog.ItemTypes.Hot,   "Espresso pha loãng với nước nóng",                 null, 40_000m, 6),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-cappuccino",      KitsMenuCatalog.Categories.Cafe, "Cappuccino",       KitsMenuCatalog.ItemTypes.Hot,   "Espresso, sữa tươi hấp, bọt mịn",                 null, 45_000m, 7, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-latte",           KitsMenuCatalog.Categories.Cafe, "Latte",            KitsMenuCatalog.ItemTypes.Hot,   "Espresso, sữa tươi hấp",                          null, 45_000m, 8, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-mocha",           KitsMenuCatalog.Categories.Cafe, "Mocha",            KitsMenuCatalog.ItemTypes.Hot,   "Espresso, socola, sữa tươi hấp",                  null, 48_000m, 9),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-cappuccino",     KitsMenuCatalog.Categories.Cafe, "Cappuccino",       KitsMenuCatalog.ItemTypes.Hot,   "Espresso, sữa tươi hấp, bọt mịn",                 null, 45_000m, 11),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}cafe-caramel-macchiato", KitsMenuCatalog.Categories.Cafe, "Caramel Macchiato", KitsMenuCatalog.ItemTypes.Iced, "Espresso, sữa tươi, sốt caramel",             null, 50_000m, 10, IsFeatured: true),

            // TRÀ (8)
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-dao-cam-sa",       KitsMenuCatalog.Categories.Tra, "Trà đào cam sả",        KitsMenuCatalog.ItemTypes.Iced, "Đào, cam, sả, trà đen",                null, 45_000m, 1, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-vai",              KitsMenuCatalog.Categories.Tra, "Trà vải",               KitsMenuCatalog.ItemTypes.Iced, "Vải tươi, trà đen",                    null, 45_000m, 2),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-chanh-mat-ong",    KitsMenuCatalog.Categories.Tra, "Trà chanh mật ong",     KitsMenuCatalog.ItemTypes.Iced, "Chanh tươi, mật ong, trà",             null, 40_000m, 3),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-dau",              KitsMenuCatalog.Categories.Tra, "Trà dâu",               KitsMenuCatalog.ItemTypes.Iced, "Dâu tươi, trà xanh",                   null, 45_000m, 4),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-xoai",             KitsMenuCatalog.Categories.Tra, "Trà xoài",              KitsMenuCatalog.ItemTypes.Iced, "Xoài tươi, trà đen",                   null, 45_000m, 5),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-o-long-sua",       KitsMenuCatalog.Categories.Tra, "Trà ô long sữa",        KitsMenuCatalog.ItemTypes.Iced, "Ô long, sữa tươi, đá",                  null, 45_000m, 6),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-earl-grey",        KitsMenuCatalog.Categories.Tra, "Trà Earl Grey",     KitsMenuCatalog.ItemTypes.Hot,  "Trà Earl Grey, chanh bergamot",         null, 40_000m, 7),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}tra-hoa-cuc-mat-ong",  KitsMenuCatalog.Categories.Tra, "Trà hoa cúc mật ong",  KitsMenuCatalog.ItemTypes.Hot,  "Hoa cúc, mật ong",                       null, 40_000m, 8),

            // BÁNH (7)
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-croissant-thit-nguoi-pho-mai", KitsMenuCatalog.Categories.Banh, "Croissant thịt nguội & phô mai", KitsMenuCatalog.ItemTypes.Food, "Croissant, thịt nguội, phô mai", null, 55_000m, 1, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-croissant-trung-muoi",         KitsMenuCatalog.Categories.Banh, "Croissant trứng muối",         KitsMenuCatalog.ItemTypes.Food, "Croissant bơ, trứng muối",      null, 50_000m, 2, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-donut-duong",                  KitsMenuCatalog.Categories.Banh, "Donut đường",                  KitsMenuCatalog.ItemTypes.Food, "Donut phủ đường",              null, 30_000m, 3),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-donut-chocolate",              KitsMenuCatalog.Categories.Banh, "Donut chocolate",              KitsMenuCatalog.ItemTypes.Food, "Donut socola",                 null, 35_000m, 4),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-donut-matcha",                 KitsMenuCatalog.Categories.Banh, "Donut matcha",                 KitsMenuCatalog.ItemTypes.Food, "Donut trà xanh matcha",       null, 35_000m, 5),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-brownie-chocolate",            KitsMenuCatalog.Categories.Banh, "Brownie chocolate",            KitsMenuCatalog.ItemTypes.Food, "Brownie socola đắng",          null, 40_000m, 6),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}banh-brownie-hat",                  KitsMenuCatalog.Categories.Banh, "Brownie hạt",                  KitsMenuCatalog.ItemTypes.Food, "Brownie với hạt rang",         null, 45_000m, 7),

            // COMBO (4)
            new($"{KitsMenuCatalog.CatalogKeyPrefix}combo-coffee-donut",   KitsMenuCatalog.Categories.Combo, "Coffee + Donut",   KitsMenuCatalog.ItemTypes.Combo, "Cà phê kèm donut",                              null, 60_000m, 1, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}combo-coffee-brownie", KitsMenuCatalog.Categories.Combo, "Coffee + Brownie", KitsMenuCatalog.ItemTypes.Combo, "Cà phê kèm brownie",                            null, 75_000m, 2, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}combo-latte-croissant", KitsMenuCatalog.Categories.Combo, "Latte + Croissant", KitsMenuCatalog.ItemTypes.Combo, "Latte kèm croissant",                          null, 90_000m, 3, IsFeatured: true),
            new($"{KitsMenuCatalog.CatalogKeyPrefix}combo-tra-donut",      KitsMenuCatalog.Categories.Combo, "Trà + Donut",      KitsMenuCatalog.ItemTypes.Combo, "Trà kèm donut",                                 null, 70_000m, 4, IsFeatured: true),
        ];
    }
}
