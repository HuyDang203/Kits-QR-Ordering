using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Annap.CoffeeQrOrdering.Web.ModelBinding;

namespace Annap.CoffeeQrOrdering.Web.ViewModels;

public sealed class MenuCurationFormModel
{
    // ── Core identity ─────────────────────────────────────────────────────────
    [Display(Name = "Drink name")]
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(240)]
    public string? Subtitle { get; set; }

    // ── Menu position ─────────────────────────────────────────────────────────
    [Required]
    public Guid CategoryId { get; set; }

    [Range(0.01, 999999.99)]
    [ModelBinder(BinderType = typeof(FlexibleDecimalModelBinder))]
    public decimal Price { get; set; }

    /// <summary>Lower numbers appear first within a category on the guest menu.</summary>
    [Range(0, 9999)]
    public int DisplaySortOrder { get; set; }

    // ── Guest-facing copy ─────────────────────────────────────────────────────
    [StringLength(2000)]
    public string? Description { get; set; }

    // ── Drink type (hot/iced) ─────────────────────────────────────────────────
    [StringLength(100)]
    public string? ItemType { get; set; }

    // ── Curation flags ────────────────────────────────────────────────────────
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsSignature { get; set; }

    // ── Images ───────────────────────────────────────────────────────────────
    /// <summary>Clear stored hero URL and delete uploaded file when applicable.</summary>
    public bool RemoveHeroImage { get; set; }

    public IFormFile? HeroImage { get; set; }

    /// <summary>Clear stored detail poster and delete uploaded file when applicable.</summary>
    public bool RemoveDetailPosterImage { get; set; }

    public IFormFile? DetailPosterImage { get; set; }

    // ── Normalisation ─────────────────────────────────────────────────────────
    /// <summary>
    /// Trims all string fields and converts whitespace-only values to null.
    /// Call before model validation so validation errors reflect cleaned values.
    /// </summary>
    public void Normalize()
    {
        Name        = Name.Trim();
        Subtitle    = NullIfWhite(Subtitle);
        Description = NullIfWhite(Description);
        ItemType    = NullIfWhite(ItemType);
    }

    private static string? NullIfWhite(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}