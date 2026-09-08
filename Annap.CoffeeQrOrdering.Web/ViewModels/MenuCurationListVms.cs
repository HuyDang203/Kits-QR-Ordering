namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed record MenuCurationPreviewVm(
    string Name,
    string? Subtitle,
    decimal Price,
    string CategoryName,
    bool IsAvailable,
    string? ImageUrl,
    string FallbackImageUrl);

public sealed record MenuCurationHeroVm(
    string Kicker,
    string Title,
    string? Lede,
    string? BackPage,
    string? BackLabel);

public sealed record MenuItemSummaryVm(
    Guid Id,
    string Name,
    string? Subtitle,
    string CategoryName,
    decimal Price,
    bool IsAvailable,
    bool IsFeatured,
    bool IsSignature,
    bool IsArchived,
    string? ImageUrl,
    string PreviewImageUrl);

public sealed record MenuCategoryGroupVm(Guid Id, string Name, IReadOnlyList<MenuItemSummaryVm> Items);