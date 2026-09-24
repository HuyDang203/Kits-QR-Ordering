namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed record RelatedDrinkVm(Guid Id, string Name, decimal Price, decimal? OriginalPrice, string? Subtitle);

/// <summary>
/// Server-rendered projection for the drink detail overlay.
///
/// <see cref="Price"/> is always the immutable base price from
/// <c>MenuItem.Price</c>. <see cref="EffectivePrice"/> is the price the guest
/// will actually pay — already adjusted for the global promotion via
/// <c>IPricingService.GetEffectivePriceAsync</c>. When the promotion is OFF
/// the two values match exactly and the detail header renders a single price.
/// </summary>
public sealed class MenuDrinkDetailVm
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required decimal EffectivePrice { get; init; }
    public required string CategoryName { get; init; }
    public string CardImageUrl { get; init; } = "";
    public string DetailPosterImageUrl { get; init; } = "";
    public IReadOnlyList<RelatedDrinkVm> Related { get; init; } = [];

    /// <summary>Quiet note when the cup cannot be added to the tray (pantry pause or availability hold).</summary>
    public string? ServiceNote { get; init; }

    // ── Display fields ────────────────────────────────────────────────────────
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public string? ItemType { get; init; }
    public bool IsSignature { get; init; }

    /// <summary>True when the drink is a coffee-family item that the catalog marks as supporting Hot/Iced.</summary>
    public bool SupportsHotIced { get; init; }
    /// <summary>Server-derived default temperature for the detail overlay.</summary>
    public string? Temperature { get; init; }
}