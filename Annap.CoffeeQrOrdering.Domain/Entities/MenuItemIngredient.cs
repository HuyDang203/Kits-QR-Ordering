using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Recipe line: quantity of ingredient consumed per one menu item sold.</summary>
public sealed class MenuItemIngredient : EntityBase
{
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    /// <summary>Amount in <see cref="Ingredient.Unit"/> for a single pour.</summary>
    public decimal QuantityRequired { get; set; } = 1m;
}
