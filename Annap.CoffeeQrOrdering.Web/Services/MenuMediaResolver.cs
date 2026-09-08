using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Single guest-facing image resolution pipeline.
/// Production durable media is Cloudinary HTTPS; local paths are development/migration only.
/// Card and detail surfaces share the same Cloudinary preference so a wiped card field
/// can still recover from <see cref="MenuItem.DetailPosterImagePath"/>.
/// </summary>
public static class MenuMediaResolver
{
    public const string FallbackPlaceholderUrl = "/images/menu-fallback.svg";

    private static Func<string, bool>? _webRootFileExists;

    public static void BindWebRootFileExists(Func<string, bool> exists) => _webRootFileExists = exists;

    /// <summary>
    /// Canonical card URL. Prefers Cloudinary on any stored field (including detail poster recovery),
    /// then managed local files.
    /// </summary>
    public static string? TryResolveCardImageUrl(
        string? cardImageUrl,
        string? heroImageUrl,
        string? imageUrl,
        string? thumbnailUrl,
        string drinkName,
        string categoryName,
        string? detailPosterImagePath = null)
    {
        foreach (var raw in new[] { thumbnailUrl, cardImageUrl, heroImageUrl, imageUrl, detailPosterImagePath })
        {
            if (IsCloudinaryUrl(raw))
                return raw!.Trim();
        }

        var sawManagedUpload = false;
        foreach (var raw in new[] { thumbnailUrl, cardImageUrl, heroImageUrl, imageUrl, detailPosterImagePath })
        {
            if (IsEphemeralPlaceholderUrl(raw))
                continue;

            if (IsManagedUploadUrl(raw))
                sawManagedUpload = true;

            var resolved = ResolveManagedCardUrl(raw);
            if (resolved is not null)
                return resolved;
        }

        if (sawManagedUpload)
            return null;

        return null;
    }

    public static string? TryResolveCardImageUrl(MenuItem item, string categoryName) =>
        TryResolveCardImageUrl(
            null,
            null,
            item.ImageUrl,
            null,
            item.Name,
            categoryName,
            item.DetailPosterImagePath);

    /// <summary>
    /// Canonical detail poster URL. Prefers Cloudinary detail poster, then Cloudinary hero,
    /// then local managed files.
    /// </summary>
    public static string? TryResolveDetailPosterUrl(
        string? detailPosterImagePath,
        string? imageUrl,
        string drinkName,
        string categoryName)
    {
        if (IsCloudinaryUrl(detailPosterImagePath))
            return detailPosterImagePath!.Trim();

        if (IsCloudinaryUrl(imageUrl))
            return imageUrl!.Trim();

        var sawManagedUpload = false;
        if (IsAllowedLocalUrl(detailPosterImagePath) && !IsEphemeralPlaceholderUrl(detailPosterImagePath))
        {
            var poster = NormalizeWebRelative(detailPosterImagePath!);
            if (MenuImagePaths.IsManagedUrl(poster))
            {
                sawManagedUpload = true;
                poster = MenuImagePaths.NormalizeToWebpUrl(poster) ?? poster;
            }
            if (WebRootExists(poster))
                return poster;
        }

        if (IsAllowedLocalUrl(imageUrl) && !IsEphemeralPlaceholderUrl(imageUrl))
        {
            var hero = NormalizeWebRelative(imageUrl!);
            if (MenuImagePaths.IsManagedUrl(hero))
            {
                sawManagedUpload = true;
                hero = MenuImagePaths.NormalizeToWebpUrl(hero) ?? hero;
            }
            if (WebRootExists(hero))
                return hero;
        }

        if (sawManagedUpload)
            return null;

        return null;
    }

    /// <summary>Legacy callers — returns empty string when unresolved.</summary>
    public static string ResolveCardImageUrl(
        string? cardImageUrl,
        string? heroImageUrl,
        string? imageUrl,
        string? thumbnailUrl,
        string drinkName,
        string categoryName,
        string? detailPosterImagePath = null) =>
        TryResolveCardImageUrl(
            cardImageUrl,
            heroImageUrl,
            imageUrl,
            thumbnailUrl,
            drinkName,
            categoryName,
            detailPosterImagePath) ?? "";

    public static string ResolveCardImageUrl(MenuItem item, string categoryName) =>
        TryResolveCardImageUrl(item, categoryName) ?? "";

    public static string ResolveDetailPosterUrl(
        string? detailPosterImagePath,
        string? imageUrl,
        string drinkName,
        string categoryName) =>
        TryResolveDetailPosterUrl(detailPosterImagePath, imageUrl, drinkName, categoryName) ?? "";

    public static bool HasLocalImage(string? url) => IsAllowedLocalUrl(url) && !IsEphemeralPlaceholderUrl(url);

    /// <summary>Cloudinary delivery HTTPS URLs only — the production durable source of truth.</summary>
    public static bool IsCloudinaryUrl(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
               && uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase)
               && uri.AbsolutePath.Contains("/image/upload/", StringComparison.Ordinal);
    }

    /// <summary>URLs that must survive redeploy: Cloudinary or managed /media uploads.</summary>
    public static bool IsDurableMediaUrl(string? url) =>
        IsCloudinaryUrl(url) || MenuImagePaths.IsManagedUrl(url);

    /// <summary>Bootstrap placeholder that must never block Cloudinary recovery.</summary>
    public static bool IsEphemeralPlaceholderUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var n = NormalizeWebRelative(url.Trim());
        return string.Equals(n, FallbackPlaceholderUrl, StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeWebRelative(string url)
    {
        var t = url.Trim();
        if (t.Length == 0)
            return t;
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return "";
        if (t.StartsWith("/", StringComparison.Ordinal))
            return t;
        return "/" + t.TrimStart('/');
    }

    private static bool IsAllowedLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var t = url.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool IsManagedUploadUrl(string? url)
    {
        if (!IsAllowedLocalUrl(url))
            return false;
        return MenuImagePaths.IsManagedUrl(NormalizeWebRelative(url!.Trim()));
    }

    private static bool IsRuntimeRasterUrl(string url)
    {
        var ext = Path.GetExtension(url);
        return string.Equals(ext, MenuImagePaths.WebpExtension, StringComparison.OrdinalIgnoreCase)
               || string.Equals(ext, ".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveManagedCardUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || IsEphemeralPlaceholderUrl(raw))
            return null;

        var t = raw.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("//", StringComparison.Ordinal)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        var normalized = NormalizeWebRelative(t);
        if (!MenuImagePaths.IsManagedUrl(normalized))
            return IsRuntimeRasterUrl(normalized) ? normalized : null;

        normalized = MenuImagePaths.NormalizeToWebpUrl(normalized) ?? normalized;

        var thumb = MenuImagePaths.DeriveThumbUrl(normalized);
        if (thumb is not null && WebRootExists(thumb))
            return thumb;

        return WebRootExists(normalized) ? normalized : null;
    }

    private static bool WebRootExists(string webRelative)
    {
        return _webRootFileExists?.Invoke(webRelative) ?? true;
    }
}
