using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Infrastructure.Services;

/// <summary>
/// Default <see cref="IPricingService"/>: reads the
/// <see cref="PricingPromotionSettings"/> singleton row and applies the discount
/// factor with whole-VND rounding (matching <c>VndMoneyFormatter.Format</c>).
///
/// All callers MUST go through <see cref="GetEffectivePrice"/> so the snapshot
/// stored on <c>OrderItem.UnitPrice</c> is identical to the price the guest
/// saw on the catalog page — preventing "display 90 000, charge 100 000" bugs.
///
/// Reads are scoped-per-call against the request DbContext; no in-memory cache.
/// Multi-instance safe because the state lives in PostgreSQL.
/// </summary>
public sealed class PricingService(IApplicationDbContext db) : IPricingService
{
    public async Task<int> GetCurrentPercentAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadSettingsAsync(cancellationToken);
        if (row is null || !row.Enabled) return 0;
        var pct = row.Percent;
        if (pct < 0) return 0;
        if (pct > 100) return 100;
        return pct;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadSettingsAsync(cancellationToken);
        return row is { Enabled: true } && row.Percent > 0 && row.Percent <= 100;
    }

    public async Task<decimal> GetEffectivePriceAsync(decimal basePrice, CancellationToken cancellationToken = default)
    {
        // Cheapest path when nothing is on: just return base price unchanged
        // without even touching the DB. Avoids a SELECT on every guest page load.
        var row = await LoadSettingsAsync(cancellationToken);
        if (row is null || !row.Enabled || row.Percent <= 0)
            return basePrice;

        // Single arithmetic path. basePrice * (100 - Percent) / 100 keeps
        // exact decimal precision for VND-friendly amounts. AwayFromZero
        // rounding matches VndMoneyFormatter.Format so the rendered price and
        // the stored UnitPrice agree exactly.
        var percent = row.Percent;
        if (percent > 100) percent = 100;
        var multiplier = (100m - percent) / 100m;
        var raw = basePrice * multiplier;
        return Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    private Task<PricingPromotionSettings?> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var sid = PricingPromotionSettingsConfiguration.SingletonId;
        return db.PricingPromotionSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
    }
}
