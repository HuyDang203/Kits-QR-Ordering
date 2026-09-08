namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed record MenuCategorySelectOption(Guid Id, string Name);

public sealed class MenuCurationEditorPartialModel
{
    public MenuCurationFormModel Input { get; set; } = new();

    public IReadOnlyList<MenuCategorySelectOption> Categories { get; set; } = Array.Empty<MenuCategorySelectOption>();

    public string? CurrentHeroUrl { get; set; }

    public string? CurrentDetailPosterUrl { get; set; }

    /// <summary>Client-side preview id (existing item id or empty Guid for create).</summary>
    public Guid PreviewCorrelationId { get; set; }

    public string FormAspPage { get; set; } = "";

    public bool IsEdit { get; set; }
}
