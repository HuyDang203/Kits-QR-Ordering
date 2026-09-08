using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Annap.CoffeeQrOrdering.Web.Services;

public enum MenuImageProfile
{
    /// <summary>Menu grid / card browse — max 480px edge.</summary>
    Thumb,

    /// <summary>Admin preview + fallback card — max 800px edge.</summary>
    Card,

    /// <summary>Detail overlay hero — max 1200px edge.</summary>
    DetailPoster
}

public sealed class MenuImageEncodeResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long Bytes { get; init; }
}

/// <summary>Server-side resize + WebP encode for mobile-safe runtime assets.</summary>
public static class MenuImagePipeline
{
    public const int ThumbMaxEdge = 480;
    public const int CardMaxEdge = 800;
    public const int DetailPosterMaxEdge = 1200;

    private static int MaxEdge(MenuImageProfile profile) => profile switch
    {
        MenuImageProfile.Thumb => ThumbMaxEdge,
        MenuImageProfile.Card => CardMaxEdge,
        MenuImageProfile.DetailPoster => DetailPosterMaxEdge,
        _ => CardMaxEdge
    };

    private static int Quality(MenuImageProfile profile) => profile switch
    {
        MenuImageProfile.Thumb => 78,
        MenuImageProfile.Card => 82,
        MenuImageProfile.DetailPoster => 84,
        _ => 82
    };

    public static async Task<MenuImageEncodeResult> EncodeToWebpFileAsync(
        Stream input,
        string outputPhysicalPath,
        MenuImageProfile profile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPhysicalPath)!);

            await using var working = new MemoryStream();
            await input.CopyToAsync(working, cancellationToken).ConfigureAwait(false);
            if (working.Length == 0)
                return Fail("Empty image stream.");

            working.Position = 0;

            using var image = await Image.LoadAsync(working, cancellationToken).ConfigureAwait(false);
            ResizeInPlace(image, MaxEdge(profile));

            var encoder = new WebpEncoder
            {
                Quality = Quality(profile),
                Method = WebpEncodingMethod.Level4,
                FileFormat = WebpFileFormatType.Lossy,
                NearLossless = false,
                TransparentColorMode = WebpTransparentColorMode.Preserve
            };

            await using var outStream = File.Create(outputPhysicalPath);
            await image.SaveAsync(outStream, encoder, cancellationToken).ConfigureAwait(false);

            var info = new FileInfo(outputPhysicalPath);
            return new MenuImageEncodeResult
            {
                Success = true,
                Width = image.Width,
                Height = image.Height,
                Bytes = info.Exists ? info.Length : 0
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    public static async Task<MenuImageEncodeResult> EncodeFileToWebpAsync(
        string inputPhysicalPath,
        string outputPhysicalPath,
        MenuImageProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var fs = File.OpenRead(inputPhysicalPath);
        return await EncodeToWebpFileAsync(fs, outputPhysicalPath, profile, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ResizeInPlace(Image image, int maxEdge)
    {
        var w = image.Width;
        var h = image.Height;
        if (w <= maxEdge && h <= maxEdge)
            return;

        var scale = Math.Min((double)maxEdge / w, (double)maxEdge / h);
        var nw = Math.Max(1, (int)Math.Round(w * scale));
        var nh = Math.Max(1, (int)Math.Round(h * scale));
        image.Mutate(x => x.Resize(nw, nh));
    }

    private static MenuImageEncodeResult Fail(string message) =>
        new() { Success = false, Error = message };
}
