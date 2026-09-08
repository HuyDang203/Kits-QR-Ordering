using Annap.CoffeeQrOrdering.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class MenuImageMigrationReport
{
    public int FilesConverted { get; set; }
    public int ThumbsGenerated { get; set; }
    public int DbPathsUpdated { get; set; }
    public int Failures { get; set; }
    public long OriginalBytes { get; set; }
    public long OptimizedBytes { get; set; }

    public double ReductionPercent =>
        OriginalBytes <= 0 ? 0 : Math.Round(100.0 * (1.0 - (double)OptimizedBytes / OriginalBytes), 1);
}

/// <summary>One-time / startup migration: convert legacy raster uploads to WebP runtime assets.</summary>
public static class MenuImageMigrationService
{
    private static readonly string[] LegacyExtensions = [".jpg", ".jpeg", ".png"];

    public static async Task<MenuImageMigrationReport> MigrateManagedUploadsAsync(
        IWebHostEnvironment env,
        IApplicationDbContext? db,
        ILogger? log,
        CancellationToken cancellationToken = default)
    {
        var report = new MenuImageMigrationReport();
        var dir = MenuImagePaths.ManagedDirectory(env);
        if (!Directory.Exists(dir))
            return report;

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var ext = Path.GetExtension(file);
            var name = Path.GetFileName(file);
            if (name.StartsWith(".", StringComparison.Ordinal))
                continue;

            if (name.Contains("-thumb", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(ext, MenuImagePaths.WebpExtension, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureThumbForHeroWebpAsync(env, file, report, log, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!LegacyExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                continue;

            var originalSize = new FileInfo(file).Length;
            report.OriginalBytes += originalSize;

            var isPoster = name.Contains("-poster", StringComparison.OrdinalIgnoreCase);
            var profile = isPoster ? MenuImageProfile.DetailPoster : MenuImageProfile.Card;
            var webpPath = Path.ChangeExtension(file, MenuImagePaths.WebpExtension)!;

            var encoded = await MenuImagePipeline.EncodeFileToWebpAsync(
                file, webpPath, profile, cancellationToken).ConfigureAwait(false);

            if (!encoded.Success)
            {
                report.Failures++;
                log?.LogWarning("Menu image migration failed for {File}: {Error}", name, encoded.Error);
                continue;
            }

            report.OptimizedBytes += encoded.Bytes;
            report.FilesConverted++;

            if (!isPoster)
            {
                var thumbPath = Path.ChangeExtension(file, null)! + "-thumb" + MenuImagePaths.WebpExtension;
                var thumb = await MenuImagePipeline.EncodeFileToWebpAsync(
                    file, thumbPath, MenuImageProfile.Thumb, cancellationToken).ConfigureAwait(false);
                if (thumb.Success)
                {
                    report.ThumbsGenerated++;
                    report.OptimizedBytes += thumb.Bytes;
                }
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                log?.LogWarning(ex, "Could not delete legacy file {File}.", name);
            }
        }

        if (db is not null)
            report.DbPathsUpdated = await NormalizeDbPathsAsync(db, log, cancellationToken).ConfigureAwait(false);

        if (report.FilesConverted > 0 || report.ThumbsGenerated > 0)
        {
            log?.LogInformation(
                "Menu image migration: converted={Converted} thumbs={Thumbs} db={Db} saved≈{Pct}% ({Before}→{After} bytes)",
                report.FilesConverted,
                report.ThumbsGenerated,
                report.DbPathsUpdated,
                report.ReductionPercent,
                report.OriginalBytes,
                report.OptimizedBytes);
        }

        return report;
    }

    public static async Task MigrateStaticAssetsAsync(
        IWebHostEnvironment env,
        ILogger? log,
        CancellationToken cancellationToken = default)
    {
        var imagesDir = Path.Combine(env.WebRootPath, "images");
        var posterJpg = Path.Combine(imagesDir, "detail-poster.jpg");
        var posterWebp = Path.Combine(imagesDir, "detail-poster.webp");

        if (!File.Exists(posterJpg))
            return;

        if (File.Exists(posterWebp))
            return;

        var encoded = await MenuImagePipeline.EncodeFileToWebpAsync(
            posterJpg, posterWebp, MenuImageProfile.DetailPoster, cancellationToken).ConfigureAwait(false);

        if (encoded.Success)
        {
            log?.LogInformation("Converted static detail-poster.jpg → webp ({Bytes} bytes).", encoded.Bytes);
            try { File.Delete(posterJpg); } catch { /* ignore */ }
        }
        else
        {
            log?.LogWarning("Failed to convert detail-poster.jpg: {Error}", encoded.Error);
        }
    }

    public static int MigrateCatalogSourceToWebp(string? sourceDirectory, string webRootPath, ILogger? log)
    {
        // Kits menu does not seed from an external asset directory — return 0.
        return 0;
    }

    private static async Task EnsureThumbForHeroWebpAsync(
        IWebHostEnvironment env,
        string heroWebpPath,
        MenuImageMigrationReport report,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(heroWebpPath);
        if (name.Contains("-poster", StringComparison.OrdinalIgnoreCase))
            return;

        var thumbPath = Path.Combine(
            Path.GetDirectoryName(heroWebpPath)!,
            Path.GetFileNameWithoutExtension(name) + "-thumb" + MenuImagePaths.WebpExtension);

        if (File.Exists(thumbPath))
            return;

        var encoded = await MenuImagePipeline.EncodeFileToWebpAsync(
            heroWebpPath, thumbPath, MenuImageProfile.Thumb, cancellationToken).ConfigureAwait(false);

        if (encoded.Success)
        {
            report.ThumbsGenerated++;
            report.OptimizedBytes += encoded.Bytes;
        }
        else
        {
            log?.LogDebug("Thumb generation skipped for {File}: {Error}", name, encoded.Error);
        }
    }

    private static async Task<int> NormalizeDbPathsAsync(
        IApplicationDbContext db,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;

        foreach (var item in items)
        {
            var newHero = MenuImagePaths.NormalizeToWebpUrl(item.ImageUrl);
            if (!string.Equals(newHero, item.ImageUrl, StringComparison.Ordinal))
            {
                item.ImageUrl = newHero;
                changed++;
            }

            var newPoster = MenuImagePaths.NormalizeToWebpUrl(item.DetailPosterImagePath);
            if (!string.Equals(newPoster, item.DetailPosterImagePath, StringComparison.Ordinal))
            {
                item.DetailPosterImagePath = newPoster;
                changed++;
            }
        }

        if (changed > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return changed;
    }
}
