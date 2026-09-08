namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>Deterministic web-relative paths for managed menu item images (WebP runtime).</summary>
public static class MenuImagePaths
{
    public const string WebRelativeFolder = "/media/menu-items";
    public const string OriginalsSubfolder = "_originals";
    public const string WebpExtension = ".webp";

    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static string ManagedDirectory(IWebHostEnvironment env) =>
        Path.Combine(env.WebRootPath, "media", "menu-items");

    public static string OriginalsDirectory(IWebHostEnvironment env) =>
        Path.Combine(ManagedDirectory(env), OriginalsSubfolder);

    public static string HeroWebRelative(Guid itemId) =>
        $"{WebRelativeFolder}/{itemId:N}{WebpExtension}";

    public static string ThumbWebRelative(Guid itemId) =>
        $"{WebRelativeFolder}/{itemId:N}-thumb{WebpExtension}";

    public static string PosterWebRelative(Guid itemId) =>
        $"{WebRelativeFolder}/{itemId:N}-poster{WebpExtension}";

    public static bool IsManagedUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith(WebRelativeFolder + "/", StringComparison.Ordinal);

    public static bool IsRasterExtension(string? ext) =>
        !string.IsNullOrEmpty(ext) && RasterExtensions.Contains(ext);

    public static string? DeriveThumbUrl(string? webRelativeUrl)
    {
        if (string.IsNullOrWhiteSpace(webRelativeUrl) || !IsManagedUrl(webRelativeUrl))
            return null;

        var fileName = Path.GetFileName(webRelativeUrl.Trim());
        if (fileName.Contains("-poster", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("-thumb", StringComparison.OrdinalIgnoreCase))
            return null;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(stem))
            return null;

        return $"{WebRelativeFolder}/{stem}-thumb{WebpExtension}";
    }

    public static string? NormalizeToWebpUrl(string? webRelativeUrl)
    {
        if (string.IsNullOrWhiteSpace(webRelativeUrl) || !IsManagedUrl(webRelativeUrl))
            return webRelativeUrl;

        var ext = Path.GetExtension(webRelativeUrl);
        if (string.Equals(ext, WebpExtension, StringComparison.OrdinalIgnoreCase))
            return webRelativeUrl;

        if (!IsRasterExtension(ext))
            return webRelativeUrl;

        return Path.ChangeExtension(webRelativeUrl, WebpExtension)?.Replace('\\', '/');
    }

    public static string? ToPhysicalPath(IWebHostEnvironment env, string? webRelativeUrl)
    {
        if (!IsManagedUrl(webRelativeUrl))
            return null;

        var name = webRelativeUrl![(WebRelativeFolder.Length + 1)..];
        if (name.Contains("..", StringComparison.Ordinal)
            || name.Contains('/', StringComparison.Ordinal)
            || name.Contains('\\', StringComparison.Ordinal))
            return null;

        return Path.Combine(ManagedDirectory(env), name);
    }
}
