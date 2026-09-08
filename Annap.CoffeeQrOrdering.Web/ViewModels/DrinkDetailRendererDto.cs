namespace Annap.CoffeeQrOrdering.Web.ViewModels;

/// <summary>JSON payload for client-side <see cref="DrinkDetailRenderer"/>.</summary>
public sealed class DrinkDetailRendererDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required decimal Price { get; init; }
    public required string PriceDisplay { get; init; }
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public bool IsSignature { get; init; }

    /// <summary>True when the menu catalog contract supports a Hot/Iced choice for this drink.</summary>
    public bool SupportsHotIced { get; init; }
    /// <summary>Default selected temperature when <see cref="SupportsHotIced"/> is true (server-derived).</summary>
    public string? Temperature { get; init; }

    // ── Runtime fields ────────────────────────────────────────────────────────
    public string? ServingNotes { get; init; }
    public required string AccentColor { get; init; }
    public string? CategoryName { get; init; }
    public bool CanAdd { get; init; } = true;
    public string? ServiceNote { get; init; }
    public bool IsBakery { get; init; }
    public IReadOnlyList<PairingSuggestionDto> Pairings { get; init; } = Array.Empty<PairingSuggestionDto>();
}