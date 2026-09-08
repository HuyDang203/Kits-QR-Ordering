using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace Annap.CoffeeQrOrdering.Web.Services;

public static class MenuHeroImageStorage
{
    public const string WebRelativeFolder = MenuImagePaths.WebRelativeFolder;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private static readonly string[] LegacyRasterExt = [".jpg", ".jpeg", ".png"];

    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Returns web-relative path to optimized card WebP or null if no file.</summary>
    public static async Task<string?> TrySaveAsync(
        IWebHostEnvironment env,
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken,
        ILogger? log = null)
    {
        if (file is null || file.Length == 0)
            return null;

        ValidateUpload(file);

        var dir = MenuImagePaths.ManagedDirectory(env);
        Directory.CreateDirectory(dir);
        PurgeHeroAssets(env, itemId);

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        var heroPhysical = Path.Combine(dir, $"{itemId:N}{MenuImagePaths.WebpExtension}");
        var thumbPhysical = Path.Combine(dir, $"{itemId:N}-thumb{MenuImagePaths.WebpExtension}");

        var card = await MenuImagePipeline.EncodeToWebpFileAsync(
            buffer, heroPhysical, MenuImageProfile.Card, cancellationToken).ConfigureAwait(false);

        if (!card.Success)
            return await FallbackCopyBufferAsync(env, buffer, file.FileName, itemId, isPoster: false, log, cancellationToken)
                .ConfigureAwait(false);

        buffer.Position = 0;
        var thumb = await MenuImagePipeline.EncodeToWebpFileAsync(
            buffer, thumbPhysical, MenuImageProfile.Thumb, cancellationToken).ConfigureAwait(false);
        if (!thumb.Success)
            log?.LogWarning("Thumb encode failed for {ItemId}: {Error}", itemId, thumb.Error);

        PurgeLegacyRasterHero(env, itemId);

        return MenuImagePaths.HeroWebRelative(itemId);
    }

    public static async Task<string?> TryPosterSaveAsync(
        IWebHostEnvironment env,
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken,
        ILogger? log = null)
    {
        if (file is null || file.Length == 0)
            return null;

        ValidateUpload(file);

        var dir = MenuImagePaths.ManagedDirectory(env);
        Directory.CreateDirectory(dir);
        PurgePosterAssets(env, itemId);

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        var posterPhysical = Path.Combine(dir, $"{itemId:N}-poster{MenuImagePaths.WebpExtension}");

        var encoded = await MenuImagePipeline.EncodeToWebpFileAsync(
            buffer, posterPhysical, MenuImageProfile.DetailPoster, cancellationToken).ConfigureAwait(false);

        if (!encoded.Success)
            return await FallbackCopyBufferAsync(env, buffer, file.FileName, itemId, isPoster: true, log, cancellationToken)
                .ConfigureAwait(false);

        PurgeLegacyRasterPoster(env, itemId);

        return MenuImagePaths.PosterWebRelative(itemId);
    }

    public static void TryDeleteIfManaged(IWebHostEnvironment env, string? imageUrl)
    {
        if (!MenuImagePaths.IsManagedUrl(imageUrl))
            return;

        if (TryParseHeroItemId(imageUrl, out var id))
            PurgeHeroAssets(env, id);
        else
            TryDeleteFile(MenuImagePaths.ToPhysicalPath(env, imageUrl));
    }

    public static void TryDeletePosterIfManaged(IWebHostEnvironment env, string? imageUrl)
    {
        if (!MenuImagePaths.IsManagedUrl(imageUrl))
            return;

        if (TryParsePosterItemId(imageUrl, out var id))
            PurgePosterAssets(env, id);
        else
            TryDeleteFile(MenuImagePaths.ToPhysicalPath(env, imageUrl));
    }

    internal static void ValidateUpload(IFormFile file)
    {
        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Image must be 5 MB or smaller.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !MenuImagePaths.IsRasterExtension(ext))
            throw new InvalidOperationException("Use JPG, PNG, or WebP.");

        var contentType = file.ContentType?.Trim() ?? "";
        if (!string.IsNullOrEmpty(contentType)
            && !AllowedContentTypes.Contains(contentType)
            && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File must be an image (JPG, PNG, or WebP).");

        var safeName = Path.GetFileName(file.FileName);
        if (!string.Equals(safeName, file.FileName, StringComparison.Ordinal)
            || safeName.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file name.");
    }

    internal static async Task ValidateImageContentAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var info = await Image.IdentifyAsync(stream, cancellationToken).ConfigureAwait(false);
            if (info is null)
                throw new InvalidOperationException("File must contain a valid JPG, PNG, or WebP image.");

            const int maxDimension = 12_000;
            const long maxPixels = 50_000_000;
            if (info.Width > maxDimension
                || info.Height > maxDimension
                || (long)info.Width * info.Height > maxPixels)
            {
                throw new InvalidOperationException("Image dimensions are too large.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "File must contain a valid JPG, PNG, or WebP image.",
                ex);
        }
    }

    private static async Task<string?> FallbackCopyBufferAsync(
        IWebHostEnvironment env,
        MemoryStream buffer,
        string originalFileName,
        Guid itemId,
        bool isPoster,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        log?.LogWarning(
            "WebP encode failed for {ItemId}; preserving original upload in _originals.",
            itemId);

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var originalsDir = MenuImagePaths.OriginalsDirectory(env);
        Directory.CreateDirectory(originalsDir);

        var suffix = isPoster ? "-poster" : "";
        var managedName = $"{itemId:N}{suffix}{ext}";
        var originalPhysical = Path.Combine(originalsDir, managedName);
        var managedPhysical = Path.Combine(MenuImagePaths.ManagedDirectory(env), managedName);

        buffer.Position = 0;
        await using (var fs = File.Create(originalPhysical))
        {
            await buffer.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        await using (var fs = File.Create(managedPhysical))
        {
            await buffer.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        return $"{WebRelativeFolder}/{managedName}";
    }

    private static void PurgeHeroAssets(IWebHostEnvironment env, Guid itemId)
    {
        var dir = MenuImagePaths.ManagedDirectory(env);
        foreach (var ext in LegacyRasterExt.Append(MenuImagePaths.WebpExtension))
            TryDeleteFile(Path.Combine(dir, $"{itemId:N}{ext}"));
        TryDeleteFile(Path.Combine(dir, $"{itemId:N}-thumb{MenuImagePaths.WebpExtension}"));
    }

    private static void PurgePosterAssets(IWebHostEnvironment env, Guid itemId)
    {
        var dir = MenuImagePaths.ManagedDirectory(env);
        foreach (var ext in LegacyRasterExt.Append(MenuImagePaths.WebpExtension))
            TryDeleteFile(Path.Combine(dir, $"{itemId:N}-poster{ext}"));
    }

    private static void PurgeLegacyRasterHero(IWebHostEnvironment env, Guid itemId)
    {
        var dir = MenuImagePaths.ManagedDirectory(env);
        foreach (var ext in LegacyRasterExt)
            TryDeleteFile(Path.Combine(dir, $"{itemId:N}{ext}"));
    }

    private static void PurgeLegacyRasterPoster(IWebHostEnvironment env, Guid itemId)
    {
        var dir = MenuImagePaths.ManagedDirectory(env);
        foreach (var ext in LegacyRasterExt)
            TryDeleteFile(Path.Combine(dir, $"{itemId:N}-poster{ext}"));
    }

    private static bool TryParseHeroItemId(string? url, out Guid id)
    {
        id = default;
        if (!MenuImagePaths.IsManagedUrl(url))
            return false;

        var name = Path.GetFileName(url!.Trim());
        if (name.Contains("-poster", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-thumb", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out id);
    }

    private static bool TryParsePosterItemId(string? url, out Guid id)
    {
        id = default;
        if (!MenuImagePaths.IsManagedUrl(url))
            return false;

        var stem = Path.GetFileNameWithoutExtension(url!.Trim());
        if (!stem.EndsWith("-poster", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParseExact(stem[..^7], "N", out id);
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
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
