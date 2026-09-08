using Annap.CoffeeQrOrdering.Domain;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Annap.CoffeeQrOrdering.Web.ViewModels;

namespace Annap.CoffeeQrOrdering.Web.Projections;

/// <summary>Canonical resolved image URLs for a drink.</summary>
/// <param name="CardUrl">Resolved URL for menu grid cards and thumbnails.</param>
/// <param name="DetailUrl">Resolved URL for the fullscreen detail overlay.</param>
/// <param name="HasLocalAsset">True when a local Annap drink image is bound.</param>
public sealed record DrinkImageSet(string CardUrl, string DetailUrl, bool HasLocalAsset);

/// <summary>
/// Single source of truth for all MenuItem ↔ ViewModel mappings.
/// Adding a new editable field: update MenuItem entity, add to Normalize() in MenuCurationFormModel,
/// then update the three methods here — done. No other files need to change.
/// </summary>
public static class MenuItemProjections
{
    // ─────────────────────────────────────────────────────────────────────────
    // Guest-facing projections
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the full editorial detail overlay ViewModel.
    /// Includes all editorial, sensory, provenance, and image data.
    /// </summary>
    public static MenuDrinkDetailVm ToEditorialDetail(
        MenuItem item,
        IReadOnlyList<RelatedDrinkVm> related,
        string? serviceNote)
    {
        var images = ToImageSet(item);
        return new MenuDrinkDetailVm
        {
            Id                   = item.Id,
            Name                 = item.Name,
            Price                = item.Price,
            CategoryName         = item.Category?.Name ?? "",
            CardImageUrl         = images.CardUrl,
            DetailPosterImageUrl = images.DetailUrl,
            Related              = related,
            ServiceNote          = serviceNote,
            Subtitle             = item.Subtitle,
            Description          = item.Description,
            ItemType             = item.ItemType,
            IsSignature          = item.IsSignature,
            SupportsHotIced      = DrinkServingTemperature.Supports(item.Category?.Name, item.Name, item.ItemType),
            Temperature          = DrinkServingTemperature.Hot,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Admin projections
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Minimal admin list card with resolved preview image.</summary>
    public static MenuItemSummaryVm ToAdminSummary(MenuItem item)
    {
        var cat     = item.Category?.Name ?? "—";
        var preview = MenuMediaResolver.ResolveCardImageUrl(null, null, item.ImageUrl, null, item.Name, cat);
        return new MenuItemSummaryVm(
            item.Id, item.Name, item.Subtitle, cat, item.Price,
            item.IsAvailable, item.IsFeatured, item.IsSignature,
            item.IsArchived,
            item.ImageUrl, preview);
    }

    /// <summary>Converts an entity to the admin curation form model for display/editing.</summary>
    public static MenuCurationFormModel ToAdminForm(MenuItem item) => new()
    {
        Name                = item.Name,
        Subtitle            = item.Subtitle,
        Description         = item.Description,
        CategoryId          = item.CategoryId,
        Price               = item.Price,
        DisplaySortOrder    = item.DisplaySortOrder,
        IsAvailable         = item.IsAvailable,
        IsFeatured          = item.IsFeatured,
        IsSignature         = item.IsSignature,
        ItemType            = item.ItemType,
    };

    /// <summary>
    /// Writes all validated form fields back to an entity in place.
    /// Used for both Create (new entity) and Edit (existing entity).
    /// </summary>
    public static void ApplyFormToEntity(MenuCurationFormModel form, MenuItem entity)
    {
        entity.Name                = form.Name.Trim();
        entity.Subtitle            = NullIfWhite(form.Subtitle);
        entity.Description         = NullIfWhite(form.Description);
        entity.CategoryId          = form.CategoryId;
        entity.Price               = decimal.Round(form.Price, 2, MidpointRounding.AwayFromZero);
        entity.DisplaySortOrder    = form.DisplaySortOrder;
        entity.IsAvailable         = form.IsAvailable;
        entity.IsFeatured          = form.IsFeatured;
        entity.IsSignature         = form.IsSignature;
        entity.ItemType            = NullIfWhite(form.ItemType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Image resolution
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the canonical image set for a drink from its stored paths.
    /// Both card and detail URLs fall back through the demo catalog if no managed
    /// image is present.
    /// </summary>
    /// <summary>Maps editorial VM to the lightweight client renderer contract.</summary>
    public static DrinkDetailRendererDto ToRendererDto(
        MenuDrinkDetailVm vm,
        IReadOnlyList<PairingSuggestionDto>? pairings = null)
    {
        var image = MenuMediaResolver.TryResolveDetailPosterUrl(
            vm.DetailPosterImageUrl,
            vm.CardImageUrl,
            vm.Name,
            vm.CategoryName) ?? "";

        var isBakery = BakeryPairingService.IsBakeryCategory(vm.CategoryName);

        return new DrinkDetailRendererDto
        {
            Id                  = vm.Id,
            Name                = vm.Name,
            Image               = image,
            Price               = vm.Price,
            PriceDisplay        = VndMoneyFormatter.Format(vm.Price),
            Subtitle            = vm.Subtitle,
            Description         = vm.Description,
            IsSignature         = vm.IsSignature,
            SupportsHotIced     = vm.SupportsHotIced,
            Temperature         = vm.Temperature,
            ServingNotes        = vm.ServiceNote,
            AccentColor         = DrinkSlugGenerator.AccentColorForCategory(vm.CategoryName),
            CategoryName        = vm.CategoryName,
            CanAdd              = string.IsNullOrWhiteSpace(vm.ServiceNote),
            ServiceNote         = vm.ServiceNote,
            IsBakery            = isBakery,
            Pairings            = pairings ?? Array.Empty<PairingSuggestionDto>(),
        };
    }

    public static DrinkImageSet ToImageSet(MenuItem item)
    {
        var cat = item.Category?.Name ?? "";
        var card = MenuMediaResolver.TryResolveCardImageUrl(item, cat) ?? "";
        var detail = MenuMediaResolver.TryResolveDetailPosterUrl(
            item.DetailPosterImagePath,
            item.ImageUrl,
            item.Name,
            cat) ?? "";
        return new DrinkImageSet(card, detail, !string.IsNullOrEmpty(card));
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static string? NullIfWhite(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

}
