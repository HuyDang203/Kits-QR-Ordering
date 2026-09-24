using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Read-only production data integrity audit (Phase 8.9B).</summary>
public sealed class ProductionDataAuditService(
    IApplicationDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IMenuInventoryGate inventoryGate,
    IPricingService pricing)
{
    /// <summary>
    /// Canonical Kits bakery category name. After the
    /// <c>MergeBakeryCategoryVariants</c> migration runs, only this exact row exists.
    /// Legacy spellings (<c>Bánh</c>, <c>Banh</c>, <c>BANH</c>) are normalized away.
    /// </summary>
    private static string CanonicalBakeryCategoryName => KitsMenuCatalog.Categories.Banh;

    /// <summary>
    /// Defensive alias kept for backward-compatible reporting — Phase 6 audit
    /// snapshots may still reference the legacy spelling. Mirrors the canonical
    /// name in the normalized database.
    /// </summary>
    private const string LegacyBakeryAlias = "Banh";

    public async Task<ProductionDataAuditReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<DataAuditFinding>();
        var dbTarget = DatabaseStartupHelper.ResolveConnectionTarget(configuration);
        var appUrlPublic = configuration["AppUrl:PublicBaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(appUrlPublic))
            appUrlPublic = null;

        string? publicBaseUrlOverride = null;
        await SafeSectionAsync(
            "Environment",
            findings,
            async () =>
            {
                var dbOverride = await db.AppNetworkSettings.AsNoTracking()
                    .Where(x => x.Id == AppNetworkSettings.SingletonId)
                    .Select(x => x.PublicBaseUrlOverride)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                publicBaseUrlOverride = string.IsNullOrWhiteSpace(dbOverride) ? null : dbOverride.Trim();
            }).ConfigureAwait(false);

        var categories = await SafeSectionAsync(
            "Category inventory",
            findings,
            () => BuildCategoryRowsAsync(cancellationToken),
            Array.Empty<DataAuditCategoryRow>()).ConfigureAwait(false);

        var bakery = await SafeSectionAsync(
            "Bakery audit",
            findings,
            () => BuildBakeryAuditAsync(cancellationToken),
            (false, null, 0, Array.Empty<string>())).ConfigureAwait(false);

        var specialty = await SafeSectionAsync(
            "Specialty audit",
            findings,
            () => BuildSpecialtyAuditAsync(cancellationToken),
            (0, Array.Empty<DataAuditSpecialtyRow>())).ConfigureAwait(false);

        var media = await SafeSectionAsync(
            "Media audit",
            findings,
            () => BuildMediaAuditAsync(cancellationToken),
            (Array.Empty<DataAuditMediaRow>(), 0, 0, 0)).ConfigureAwait(false);

        var pairing = await SafeSectionAsync(
            "Pairing audit",
            findings,
            () => BuildPairingSampleAsync(cancellationToken),
            (DataAuditPairingSample?)null).ConfigureAwait(false);

        var totalMenuItems = await SafeSectionAsync(
            "Menu item count",
            findings,
            () => db.MenuItems.AsNoTracking().CountAsync(cancellationToken),
            0).ConfigureAwait(false);

        var diagnostics = new DataAuditDiagnostics(
            categories.Count,
            totalMenuItems,
            bakery.CategoryExists ? 1 : 0,
            bakery.ItemCount,
            media.PosterFileCount);

        AppendCategoryFindings(categories, findings);
        AppendBakeryFindings(bakery, findings);
        AppendSpecialtyFindings(specialty.PoolCount, specialty.Rows, findings);
        AppendMediaFindings(media.MissingImagePaths, media.MissingPosterPaths, findings);
        AppendPairingFindings(pairing, findings);

        var overall = findings.Count == 0
            ? DataAuditLevel.Pass
            : findings.Any(f => f.Level == DataAuditLevel.Fail)
                ? DataAuditLevel.Fail
                : DataAuditLevel.Warn;

        if (overall == DataAuditLevel.Pass)
            findings.Add(new DataAuditFinding(DataAuditLevel.Pass, "All audited checks passed."));

        return new ProductionDataAuditReport(
            diagnostics,
            categories,
            bakery.CategoryExists,
            bakery.CategoryName,
            bakery.ItemCount,
            bakery.ItemNames,
            specialty.PoolCount,
            specialty.Rows,
            media.Rows,
            media.MissingImagePaths,
            media.MissingPosterPaths,
            pairing,
            dbTarget.Host,
            dbTarget.Database,
            appUrlPublic,
            publicBaseUrlOverride,
            InfrastructureEnvironment.IsRenderDeployment,
            overall,
            findings);
    }

    private static async Task SafeSectionAsync(
        string sectionName,
        List<DataAuditFinding> findings,
        Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{sectionName} audit failed: {Condense(ex.Message)}"));
        }
    }

    private static async Task<T> SafeSectionAsync<T>(
        string sectionName,
        List<DataAuditFinding> findings,
        Func<Task<T>> action,
        T fallback)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{sectionName} audit failed: {Condense(ex.Message)}"));
            return fallback;
        }
    }

    private static string Condense(string message)
    {
        var t = (message ?? "").Trim();
        if (t.Length <= 180)
            return t;
        return t[..177] + "...";
    }

    private async Task<IReadOnlyList<DataAuditCategoryRow>> BuildCategoryRowsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Name,
                c.SortOrder,
                TotalItems = c.Items.Count,
                AvailableItems = c.Items.Count(i => i.IsAvailable && !i.IsArchived),
                ArchivedItems = c.Items.Count(i => i.IsArchived)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new DataAuditCategoryRow(
                r.Name,
                r.SortOrder,
                r.TotalItems,
                r.AvailableItems,
                r.ArchivedItems))
            .ToList();
    }

    private async Task<(bool CategoryExists, string? CategoryName, int ItemCount, IReadOnlyList<string> ItemNames)> BuildBakeryAuditAsync(
        CancellationToken cancellationToken)
    {
        var bakeryCategory = await db.MenuCategories.AsNoTracking()
            .Where(c => c.Name == CanonicalBakeryCategoryName || c.Name == LegacyBakeryAlias)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bakeryCategory is null)
            return (false, null, 0, Array.Empty<string>());

        var items = await db.MenuItems.AsNoTracking()
            .Where(i =>
                i.Category != null
                && (i.Category.Name == CanonicalBakeryCategoryName || i.Category.Name == LegacyBakeryAlias))
            .OrderBy(i => i.Name)
            .Select(i => i.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (true, bakeryCategory, items.Count, items);
    }

            /// <summary>
    /// Verifies the canonical Kits catalog keys are present. Does not enforce the full 29-item
    /// count — admin may archive items. Replaces the old "Annap specialty" audit.
    /// </summary>
    private async Task<(int PoolCount, IReadOnlyList<DataAuditSpecialtyRow> Rows)> BuildSpecialtyAuditAsync(
        CancellationToken cancellationToken)
    {
        var kitsPrefix = KitsMenuCatalog.CatalogKeyPrefix;
        var availableKitsCount = await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable && !m.IsArchived)
            .Where(m => m.CatalogKey != null && m.CatalogKey.StartsWith(kitsPrefix))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await db.MenuItems.AsNoTracking()
            .Where(m => m.CatalogKey != null && m.CatalogKey.StartsWith(kitsPrefix))
            .Select(m => new DataAuditSpecialtyRow(
                m.CatalogKey!,
                m.Name,
                m.IsAvailable,
                m.IsArchived,
                m.IsSignature))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (availableKitsCount, rows);
    }

    private async Task<(IReadOnlyList<DataAuditMediaRow> Rows, int MissingImagePaths, int MissingPosterPaths, int PosterFileCount)> BuildMediaAuditAsync(
        CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.AsNoTracking()
            .OrderBy(i => i.Name)
            .Select(i => new { i.Name, i.ImageUrl, i.DetailPosterImagePath })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<DataAuditMediaRow>();
        var missingImages = 0;
        var missingPosters = 0;
        var posterFilesFound = 0;

        foreach (var item in items)
        {
            var imageExists = ResolveLocalFileExists(item.ImageUrl);
            var posterExists = ResolveLocalFileExists(item.DetailPosterImagePath);

            if (imageExists == false)
                missingImages++;
            if (posterExists == false)
                missingPosters++;
            if (posterExists == true)
                posterFilesFound++;

            rows.Add(new DataAuditMediaRow(
                item.Name,
                item.ImageUrl,
                imageExists,
                item.DetailPosterImagePath,
                posterExists,
                imageExists == false || posterExists == false));
        }

        return (rows, missingImages, missingPosters, posterFilesFound);
    }

    private async Task<DataAuditPairingSample?> BuildPairingSampleAsync(CancellationToken cancellationToken)
    {
        var blocked = await inventoryGate.GetStockBlockedMenuItemIdsAsync(cancellationToken).ConfigureAwait(false);

        var bakeryCandidates = await db.MenuItems.AsNoTracking()
            .Where(i =>
                i.Category != null
                && (i.Category.Name == CanonicalBakeryCategoryName || i.Category.Name == LegacyBakeryAlias)
                && i.IsAvailable
                && !i.IsArchived)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bakeryPoolCount = bakeryCandidates.Count(id => !blocked.Contains(id));

        var drinkCandidates = await db.MenuItems.AsNoTracking()
            .Where(i =>
                i.IsAvailable
                && !i.IsArchived
                && i.Category != null
                && i.Category.Name != CanonicalBakeryCategoryName
                && i.Category.Name != LegacyBakeryAlias)
            .OrderBy(i => i.Category!.SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, CategoryName = i.Category!.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sampleDrink = drinkCandidates.FirstOrDefault(d => !blocked.Contains(d.Id));
        if (sampleDrink is null)
            return null;

        var pairings = await BakeryPairingService.GetSuggestionsForDrinkAsync(
            db,
            inventoryGate,
            pricing,
            sampleDrink.Id,
            sampleDrink.CategoryName,
            cancellationToken).ConfigureAwait(false);

        return new DataAuditPairingSample(
            sampleDrink.Name,
            sampleDrink.Id,
            bakeryPoolCount,
            pairings.Count,
            pairings.Select(p => p.Name).ToList());
    }

    private bool? ResolveLocalFileExists(string? webRelativeUrl)
    {
        if (string.IsNullOrWhiteSpace(webRelativeUrl))
            return null;

        var trimmed = webRelativeUrl.Trim();
        if (MenuMediaResolver.IsCloudinaryUrl(trimmed))
            return true;

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("//", StringComparison.Ordinal))
            return null;

        if (MenuImagePaths.IsManagedUrl(trimmed))
        {
            var normalized = MenuImagePaths.NormalizeToWebpUrl(trimmed) ?? trimmed;
            var physical = MenuImagePaths.ToPhysicalPath(environment, normalized);
            return physical is not null && File.Exists(physical);
        }

        if (!trimmed.StartsWith('/'))
            trimmed = "/" + trimmed;

        var rel = trimmed.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (rel.Contains("..", StringComparison.Ordinal))
            return null;

        var path = Path.Combine(environment.WebRootPath, rel);
        return File.Exists(path);
    }

    private static void AppendCategoryFindings(
        IReadOnlyList<DataAuditCategoryRow> categories,
        List<DataAuditFinding> findings)
    {
        if (categories.Count == 0)
            findings.Add(new DataAuditFinding(DataAuditLevel.Fail, "No menu categories found."));
    }

    private static void AppendBakeryFindings(
        (bool CategoryExists, string? CategoryName, int ItemCount, IReadOnlyList<string> ItemNames) bakery,
        List<DataAuditFinding> findings)
    {
        if (!bakery.CategoryExists)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Fail,
                $"Bakery category missing (expected \"{KitsMenuCatalog.Categories.Banh}\")."));
            return;
        }

        if (bakery.ItemCount == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"Bakery category \"{bakery.CategoryName}\" exists but contains 0 items."));
        }
    }

    private static void AppendPairingFindings(DataAuditPairingSample? pairing, List<DataAuditFinding> findings)
    {
        if (pairing is null)
            return;

        if (pairing.BakeryPoolCount == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                "Pairing audit: bakery pool has 0 available items — drink detail pairings will be empty."));
        }
        else if (pairing.PairingsReturned == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"Pairing audit: sample drink \"{pairing.DrinkName}\" returned 0 pairings despite bakery pool of {pairing.BakeryPoolCount}."));
        }
    }

    private static void AppendSpecialtyFindings(
        int poolCount,
        IReadOnlyList<DataAuditSpecialtyRow> rows,
        List<DataAuditFinding> findings)
    {
        if (poolCount == 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Fail,
                "No Kits-prefixed menu items are currently available — run KitsMenuBootstrap or check catalog keys."));
        }
        else if (poolCount < 29)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"Kits catalog available items={poolCount}, expected 29 (canonical seeded menu)."));
        }
    }

    private static void AppendMediaFindings(
        int missingImagePaths,
        int missingPosterPaths,
        List<DataAuditFinding> findings)
    {
        if (missingImagePaths > 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{missingImagePaths} ImageUrl path(s) reference missing files on disk."));
        }

        if (missingPosterPaths > 0)
        {
            findings.Add(new DataAuditFinding(
                DataAuditLevel.Warn,
                $"{missingPosterPaths} DetailPosterImagePath value(s) reference missing files on disk."));
        }
    }
}
