using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Services;

/// <summary>
/// Stores menu uploads in Cloudinary when configured. Local storage remains available
/// only for development so existing local workflows keep working without cloud credentials.
/// Production startup validation requires Cloudinary configuration.
/// </summary>
public sealed class MenuImageStorage(
    IWebHostEnvironment environment,
    IOptions<CloudinaryOptions> options,
    ILogger<MenuImageStorage> logger) : IMenuImageStorage
{
    private readonly CloudinaryOptions _options = options.Value;
    private readonly Cloudinary? _cloudinary = CreateClient(options.Value);

    public Task<string?> SaveHeroAsync(
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken = default) =>
        SaveAsync(file, itemId, isPoster: false, cancellationToken);

    public Task<string?> SavePosterAsync(
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken = default) =>
        SaveAsync(file, itemId, isPoster: true, cancellationToken);

    public async Task DeleteHeroAsync(
        Guid itemId,
        string? currentUrl,
        CancellationToken cancellationToken = default)
    {
        await DeleteManagedAsync(
            itemId,
            currentUrl,
            isPoster: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosterAsync(
        Guid itemId,
        string? currentUrl,
        CancellationToken cancellationToken = default)
    {
        await DeleteManagedAsync(
            itemId,
            currentUrl,
            isPoster: true,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteManagedAsync(
        Guid itemId,
        string? currentUrl,
        bool isPoster,
        CancellationToken cancellationToken)
    {
        if (MenuImagePaths.IsManagedUrl(currentUrl))
        {
            if (isPoster)
                MenuHeroImageStorage.TryDeletePosterIfManaged(environment, currentUrl);
            else
                MenuHeroImageStorage.TryDeleteIfManaged(environment, currentUrl);
            return;
        }

        if (_cloudinary is null)
            return;

        var publicId = TryGetCloudinaryPublicId(currentUrl) ?? PublicId(itemId, isPoster);
        await DeleteCloudinaryAssetAsync(publicId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> SaveAsync(
        IFormFile? file,
        Guid itemId,
        bool isPoster,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return null;

        MenuHeroImageStorage.ValidateUpload(file);
        await MenuHeroImageStorage.ValidateImageContentAsync(file, cancellationToken)
            .ConfigureAwait(false);

        if (_cloudinary is null)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Cloudinary image storage is not configured.");

            return isPoster
                ? await MenuHeroImageStorage.TryPosterSaveAsync(
                    environment, file, itemId, cancellationToken, logger).ConfigureAwait(false)
                : await MenuHeroImageStorage.TrySaveAsync(
                    environment, file, itemId, cancellationToken, logger).ConfigureAwait(false);
        }

        try
        {
            await using var input = file.OpenReadStream();
            var maxEdge = isPoster
                ? MenuImagePipeline.DetailPosterMaxEdge
                : MenuImagePipeline.CardMaxEdge;

            var upload = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, input),
                PublicId = PublicId(itemId, isPoster),
                Overwrite = true,
                Invalidate = true,
                UniqueFilename = false,
                UseFilename = false,
                Format = "webp",
                Transformation = new Transformation()
                    .Width(maxEdge)
                    .Height(maxEdge)
                    .Crop("limit")
                    .Quality("auto:good")
            };

            var result = await _cloudinary.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
            if (result.Error is not null || result.SecureUrl is null)
            {
                var detail = result.Error?.Message ?? "Cloudinary returned no secure URL.";
                logger.LogError(
                    "Cloudinary menu image upload failed for {ItemId} ({Kind}): {Error}",
                    itemId,
                    isPoster ? "poster" : "hero",
                    detail);
                throw new InvalidOperationException("Image upload failed. Please try again.");
            }

            logger.LogInformation(
                "Uploaded menu image to Cloudinary for {ItemId} ({Kind}); publicId={PublicId}",
                itemId,
                isPoster ? "poster" : "hero",
                result.PublicId);

            return result.SecureUrl.AbsoluteUri;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Cloudinary menu image upload failed for {ItemId} ({Kind})",
                itemId,
                isPoster ? "poster" : "hero");
            throw new InvalidOperationException("Image upload failed. Please try again.");
        }
    }

    private async Task DeleteCloudinaryAssetAsync(string publicId, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _cloudinary!.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image,
                Invalidate = true
            }).ConfigureAwait(false);

            if (result.Error is not null)
            {
                logger.LogWarning(
                    "Cloudinary menu image delete failed for {PublicId}: {Error}",
                    publicId,
                    result.Error.Message);
                return;
            }

            logger.LogInformation(
                "Deleted Cloudinary menu image {PublicId}; result={Result}",
                publicId,
                result.Result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Database cleanup must still proceed; a failed remote delete is logged for operations.
            logger.LogWarning(ex, "Cloudinary menu image delete failed for {PublicId}", publicId);
        }
    }

    private string PublicId(Guid itemId, bool isPoster)
    {
        var folder = (_options.Folder ?? "").Trim().Trim('/');
        var name = $"{itemId:N}{(isPoster ? "-poster" : "")}";
        return string.IsNullOrEmpty(folder) ? name : $"{folder}/{name}";
    }

    internal static string? TryGetCloudinaryPublicId(string? url)
    {
        if (!MenuMediaResolver.IsCloudinaryUrl(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        const string uploadMarker = "/image/upload/";
        var marker = uri.AbsolutePath.IndexOf(uploadMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        var remainder = uri.AbsolutePath[(marker + uploadMarker.Length)..].Trim('/');
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segments.Count == 0)
            return null;

        if (segments[0].Length > 1
            && segments[0][0] == 'v'
            && segments[0].AsSpan(1).ToString().All(char.IsDigit))
        {
            segments.RemoveAt(0);
        }

        if (segments.Count == 0)
            return null;

        var last = Uri.UnescapeDataString(segments[^1]);
        var extension = Path.GetExtension(last);
        if (!string.IsNullOrEmpty(extension))
            last = last[..^extension.Length];
        segments[^1] = last;

        return string.Join('/', segments.Select(Uri.UnescapeDataString));
    }

    internal static bool ShouldDeleteSupersededUrl(string? previousUrl, string? newUrl)
    {
        if (string.IsNullOrWhiteSpace(previousUrl) || string.IsNullOrWhiteSpace(newUrl))
            return false;

        if (string.Equals(previousUrl.Trim(), newUrl.Trim(), StringComparison.Ordinal))
            return false;

        if (MenuImagePaths.IsManagedUrl(previousUrl))
            return true;

        var previousPublicId = TryGetCloudinaryPublicId(previousUrl);
        var newPublicId = TryGetCloudinaryPublicId(newUrl);
        return previousPublicId is not null
               && newPublicId is not null
               && !string.Equals(previousPublicId, newPublicId, StringComparison.Ordinal);
    }

    private static Cloudinary? CreateClient(CloudinaryOptions options)
    {
        if (!options.IsConfigured)
            return null;

        var cloudinary = new Cloudinary(new Account(
            options.CloudName.Trim(),
            options.ApiKey.Trim(),
            options.ApiSecret.Trim()));
        cloudinary.Api.Secure = true;
        return cloudinary;
    }
}
