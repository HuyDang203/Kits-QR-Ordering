using Annap.CoffeeQrOrdering.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Production maintenance for managed menu images: orphans, missing assets, thumb rebuild.</summary>
public sealed class MenuMediaMaintenanceService(
    IApplicationDbContext db,
    IWebHostEnvironment env,
    IMenuImageStorage imageStorage,
    ILogger<MenuMediaMaintenanceService> log)
{
    public sealed record MaintenanceReport(
        int OrphansRemoved,
        int ThumbsRebuilt,
        int MissingReferenced,
        IReadOnlyList<string> MissingUrls);

    public async Task<MaintenanceReport> RunAsync(
        bool removeOrphans = true,
        bool rebuildMissingThumbs = true,
        CancellationToken cancellationToken = default)
    {
        var referenced = await CollectReferencedUrlsAsync(cancellationToken).ConfigureAwait(false);
        var referencedFiles = referenced
            .Select(u => Path.GetFileName(MenuImagePaths.ToPhysicalPath(env, u) ?? ""))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dir = MenuImagePaths.ManagedDirectory(env);
        var orphansRemoved = 0;
        if (removeOrphans && Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith('_') || referencedFiles.Contains(name))
                    continue;

                if (IsManagedFileName(name))
                {
                    TryDelete(file);
                    orphansRemoved++;
                    log.LogInformation("Removed orphan menu image {File}", name);
                }
            }
        }

        var thumbsRebuilt = 0;
        var missing = new List<string>();
        if (rebuildMissingThumbs)
        {
            foreach (var url in referenced)
            {
                if (!MenuImagePaths.IsManagedUrl(url))
                    continue;

                var physical = MenuImagePaths.ToPhysicalPath(env, url);
                if (physical is null || !File.Exists(physical))
                {
                    missing.Add(url);
                    continue;
                }

                var thumbUrl = MenuImagePaths.DeriveThumbUrl(url);
                if (thumbUrl is null)
                    continue;

                var thumbPhysical = MenuImagePaths.ToPhysicalPath(env, thumbUrl);
                if (thumbPhysical is not null && File.Exists(thumbPhysical))
                    continue;

                if (!url.EndsWith(MenuImagePaths.WebpExtension, StringComparison.OrdinalIgnoreCase)
                    || url.Contains("-poster", StringComparison.OrdinalIgnoreCase)
                    || url.Contains("-thumb", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    await using var fs = File.OpenRead(physical);
                    var encoded = await MenuImagePipeline.EncodeToWebpFileAsync(
                        fs,
                        thumbPhysical!,
                        MenuImageProfile.Thumb,
                        cancellationToken).ConfigureAwait(false);
                    if (encoded.Success)
                        thumbsRebuilt++;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Thumb rebuild failed for {Url}", url);
                }
            }
        }

        if (orphansRemoved > 0 || thumbsRebuilt > 0 || missing.Count > 0)
        {
            log.LogInformation(
                "Menu media maintenance: orphans={Orphans} thumbs={Thumbs} missing={Missing}",
                orphansRemoved,
                thumbsRebuilt,
                missing.Count);
        }

        return new MaintenanceReport(orphansRemoved, thumbsRebuilt, missing.Count, missing);
    }

    public async Task PurgeManagedAssetsForItemAsync(
        Guid itemId,
        string? heroUrl,
        string? posterUrl,
        CancellationToken cancellationToken = default)
    {
        await imageStorage.DeleteHeroAsync(itemId, heroUrl, cancellationToken).ConfigureAwait(false);
        await imageStorage.DeletePosterAsync(itemId, posterUrl, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HashSet<string>> CollectReferencedUrlsAsync(CancellationToken cancellationToken)
    {
        var urls = await db.MenuItems.AsNoTracking()
            .Select(i => new { i.ImageUrl, i.DetailPosterImagePath })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in urls)
        {
            if (MenuImagePaths.IsManagedUrl(row.ImageUrl))
                set.Add(row.ImageUrl!);
            if (MenuImagePaths.IsManagedUrl(row.DetailPosterImagePath))
                set.Add(row.DetailPosterImagePath!);
            var thumb = MenuImagePaths.DeriveThumbUrl(row.ImageUrl);
            if (thumb is not null)
                set.Add(thumb);
        }

        return set;
    }

    private static bool IsManagedFileName(string name) =>
        name.EndsWith(MenuImagePaths.WebpExtension, StringComparison.OrdinalIgnoreCase)
        || MenuImagePaths.IsRasterExtension(Path.GetExtension(name));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }
}
