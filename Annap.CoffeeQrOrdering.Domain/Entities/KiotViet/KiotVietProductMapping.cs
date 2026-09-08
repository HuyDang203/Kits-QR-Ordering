using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;

/// <summary>Maps an ANNAP <see cref="MenuItem"/> to a KiotViet product code (pricing sync uses this in later phases).</summary>
public sealed class KiotVietProductMapping
{
    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public string KiotVietProductCode { get; set; } = null!;

    public string? KiotVietProductName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset LastSyncedAtUtc { get; set; }

    public string? SyncNote { get; set; }
}
