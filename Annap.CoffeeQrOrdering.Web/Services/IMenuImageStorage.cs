namespace Annap.CoffeeQrOrdering.Web.Services;

public interface IMenuImageStorage
{
    Task<string?> SaveHeroAsync(
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<string?> SavePosterAsync(
        IFormFile? file,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task DeleteHeroAsync(
        Guid itemId,
        string? currentUrl,
        CancellationToken cancellationToken = default);

    Task DeletePosterAsync(
        Guid itemId,
        string? currentUrl,
        CancellationToken cancellationToken = default);
}
