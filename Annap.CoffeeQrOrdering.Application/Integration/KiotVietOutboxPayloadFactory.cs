using System.Text.Json;
using System.Text.Json.Serialization;
using Annap.CoffeeQrOrdering.Domain;
using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Application.Integration;

/// <summary>
/// Builds the immutable JSON payload stored in <see cref="Domain.Entities.KiotViet.KiotVietOutboxMessage.Payload"/>.
/// Call after <see cref="Order.RecalculateTotals"/> and before SaveChanges so all prices are finalised.
/// </summary>
public static class KiotVietOutboxPayloadFactory
{
    private static readonly JsonSerializerOptions s_opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Build(
        Order order,
        VenueTable table,
        IReadOnlyDictionary<Guid, MenuItem> menuItems,
        DateTimeOffset snapshotAt)
    {
        var payload = new KiotVietOrderPayload
        {
            OrderId = order.Id,
            VenueTableId = table.Id,
            TableCode = table.DisplayCode,
            KiotVietTableId = table.KiotVietTableId,
            KiotVietBranchId = table.KiotVietBranchId,
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = snapshotAt,
            Items = order.Items.Select(item =>
            {
                var name = menuItems[item.MenuItemId].Name;
                var temp = DrinkServingTemperature.Normalize(item.Temperature);
                if (temp != null)
                    name = $"{name} ({temp})";
                return new KiotVietOrderPayloadLine
                {
                    MenuItemId = item.MenuItemId,
                    Name = name,
                    CatalogKey = menuItems[item.MenuItemId].CatalogKey,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Notes = item.Notes
                };
            }).ToList()
        };

        return JsonSerializer.Serialize(payload, s_opts);
    }
}
