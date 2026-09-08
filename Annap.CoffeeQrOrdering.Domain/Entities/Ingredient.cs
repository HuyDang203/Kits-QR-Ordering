using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Stocked pantry item used by one or more menu recipes.</summary>
public sealed class Ingredient : AuditableEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Display unit (g, ml, piece, etc.).</summary>
    public string Unit { get; set; } = "unit";

    public decimal CurrentStock { get; set; }

    public decimal LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public List<MenuItemIngredient> MenuLinks { get; set; } = [];
}
