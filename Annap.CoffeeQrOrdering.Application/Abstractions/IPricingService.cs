namespace Annap.CoffeeQrOrdering.Application.Abstractions;

/// <summary>
/// Single source of truth for "what should the guest pay for this base price?"
///
/// Reads the global <see cref="Domain.Entities.PricingPromotionSettings"/> singleton
/// (DB) and applies the discount factor when enabled. The exact same instance
/// must be used by:
///
///   1. Guest catalog display (<c>Menu/Index.cshtml</c>, <c>/api/menu</c>)
///   2. Order submit (<c>POST /api/orders</c>) — captures <c>OrderItem.UnitPrice</c>
///
/// so that the displayed price and the persisted snapshot agree. Cart, staff
/// board, and bank QR all read from the snapshot or the catalog-derived price
/// and therefore inherit the same effective price automatically.
///
/// The result is rounded to whole VND with <c>MidpointRounding.AwayFromZero</c>
/// to match <c>VndMoneyFormatter.Format</c>, so a base price of 39 000 ₫ with
/// 10 % off becomes exactly 35 100 ₫ (no fractional đồng stored anywhere).
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Apply the current promotion settings to a base price.
    /// Returns <paramref name="basePrice"/> unchanged when the promotion is OFF.
    /// Returns <c>Math.Round(basePrice * (100 - Percent) / 100, MidpointRounding.AwayFromZero)</c>
    /// when the promotion is ON.
    /// </summary>
    Task<decimal> GetEffectivePriceAsync(decimal basePrice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshot of the current promotion state — useful for catalog rendering
    /// and UI affordances ("show strike-through original price only when this is
    /// non-zero").
    /// </summary>
    /// <returns>Current percent off (0 when disabled).</returns>
    Task<int> GetCurrentPercentAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the global promotion is currently ON.</summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
}
