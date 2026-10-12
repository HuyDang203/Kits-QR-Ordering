using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class MenuItem : AuditableEntity
{
    public Guid CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// Optional English name for the drink. When the guest UI is in English and
    /// this value contains non-whitespace text, it is rendered instead of
    /// <see cref="Name"/>. When null/blank, the Vietnamese <see cref="Name"/>
    /// is the fallback. Snapshotting is the caller's responsibility.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "English drink name")]
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? NameEn { get; set; }

    /// <summary>Optional stable key from the owner catalog export (slug); used for idempotent imports.</summary>
    public string? CatalogKey { get; set; }

    /// <summary>Loose drink "type" from the house catalog (e.g. hot, iced, ritual) — display + filters.</summary>
    public string? ItemType { get; set; }

    /// <summary>Short line under the name on cards and detail (e.g. origin, format).</summary>
    public string? Subtitle { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    /// <summary>Lower sorts first within a category (guest menu, QR rails).</summary>
    public int DisplaySortOrder { get; set; }

    public bool IsAvailable { get; set; } = true;

    /// <summary>When true, hidden from guest menu and ordering; retained for the line sheet.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Surface on home or curated rails.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>House signature spotlight (home, storytelling).</summary>
    public bool IsSignature { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Web-relative path to the fullscreen detail poster image (admin upload). Falls back to ImageUrl when absent.</summary>
    public string? DetailPosterImagePath { get; set; }

    public List<MenuItemIngredient> RecipeLines { get; set; } = [];
}