using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>
/// Singleton row (fixed id) for the global "Giảm giá 10%" promotion toggle.
/// One row total. Server-authoritative source of truth for the discount state —
/// consumed by <c>IPricingService.GetEffectivePrice</c> at catalog render and
/// at order submit time. Never mutated client-side.
/// </summary>
public sealed class PricingPromotionSettings : AuditableEntity
{
    /// <summary>
    /// When true, every menu item's <c>Price</c> is reduced by
    /// <see cref="Percent"/> percent on display and at order submit time.
    /// Default is false so a fresh deploy never applies a discount.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Integer percent off applied when <see cref="Enabled"/> is true.
    /// Whole-number only — the value is "10" for the "Giảm giá 10%" UX,
    /// never a fractional percent. Clamped to [0, 100].
    /// </summary>
    public int Percent { get; set; } = 10;
}
