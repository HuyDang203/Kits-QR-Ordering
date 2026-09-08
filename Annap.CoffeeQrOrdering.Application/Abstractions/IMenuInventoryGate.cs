namespace Annap.CoffeeQrOrdering.Application.Abstractions;

/// <summary>Pantry-aware sellability: menu items blocked when recipe stock is short.</summary>
public interface IMenuInventoryGate
{
    /// <summary>Menu item IDs that cannot be sold due to inactive ingredients or insufficient stock.</summary>
    Task<IReadOnlySet<Guid>> GetStockBlockedMenuItemIdsAsync(CancellationToken cancellationToken = default);
}
