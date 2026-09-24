namespace Annap.CoffeeQrOrdering.Web.ViewModels;

/// <summary>
/// Bakery item suggested alongside a drink detail view.
///
/// <see cref="Price"/> is the effective price (already discounted when the
/// global promotion is ON); <see cref="OriginalPrice"/> is the immutable base
/// from <c>MenuItem.Price</c> so the renderer can strike through when the
/// two values diverge.
/// </summary>
public sealed class PairingSuggestionDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required decimal Price { get; init; }
    public required decimal OriginalPrice { get; init; }
    public required string PriceDisplay { get; init; }
    public string? OriginalPriceDisplay { get; init; }
}
