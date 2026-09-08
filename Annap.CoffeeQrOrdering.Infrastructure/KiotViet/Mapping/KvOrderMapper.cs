using Annap.CoffeeQrOrdering.Application.Integration;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Dtos;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Mapping;

internal static class KvOrderMapper
{
    /// <summary>
    /// Maps the immutable ANNAP outbox snapshot to a KiotViet create-order request.
    /// branchId resolution order: per-table override → global config.
    /// Table context is placed in description ("Bàn {tableCode}") — KiotViet has no native tableId field.
    /// </summary>
    public static KvCreateOrderRequest Map(KiotVietOrderPayload snapshot, KiotVietOptions opts) =>
        new()
        {
            BranchId = snapshot.KiotVietBranchId ?? opts.BranchId,
            Description = $"Bàn {snapshot.TableCode}",
            OrderDetails = snapshot.Items.Select(item => new KvCreateOrderDetailRequest
            {
                ProductCode = item.CatalogKey,
                ProductName = item.Name,
                Quantity = (double)item.Quantity,
                Price = item.UnitPrice,
                Note = item.Notes
            }).ToList()
        };
}
