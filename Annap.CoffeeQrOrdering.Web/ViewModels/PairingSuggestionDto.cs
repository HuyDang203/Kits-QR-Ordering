namespace Annap.CoffeeQrOrdering.Web.ViewModels;

/// <summary>Bakery item suggested alongside a drink detail view.</summary>
public sealed class PairingSuggestionDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required decimal Price { get; init; }
    public required string PriceDisplay { get; init; }
}
