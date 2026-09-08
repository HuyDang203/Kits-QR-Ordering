using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class OrderItem : EntityBase
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }

    /// <summary>Guest preparation note for this line (e.g. less ice).</summary>
    public string? CustomerNote { get; set; }

    /// <summary>Serving temperature when the drink supports Hot/Iced: "Hot" | "Iced".</summary>
    public string? Temperature { get; set; }

    /// <summary>
    /// Snapshot of the menu item name at order submit time so historical tickets remain readable
    /// even when the menu is renamed or archived later.
    /// </summary>
    public string? MenuItemName { get; set; }

    /// <summary>How many units of this line have been marked prepared by barista (0..Quantity).</summary>
    public int PreparedQuantity { get; set; }

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public string? PreparedBy { get; set; }

    /// <summary>Individual staff account that prepared this line (null for shared barista).</summary>
    public Guid? PreparedByAccountId { get; set; }
}

