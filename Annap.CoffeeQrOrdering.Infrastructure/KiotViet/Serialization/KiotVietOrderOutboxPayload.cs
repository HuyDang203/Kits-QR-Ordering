using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Serialization;

/// <summary>JSON snapshot stored in <see cref="Domain.Entities.KiotViet.KiotVietOutboxMessage.Payload"/>.</summary>
public sealed class KiotVietOrderOutboxPayload
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }

    [JsonPropertyName("tableCode")]
    public string TableCode { get; set; } = null!;

    [JsonPropertyName("kiotvietTableId")]
    public string? KiotVietTableId { get; set; }

    [JsonPropertyName("kiotvietBranchIdOverride")]
    public int? KiotVietBranchIdOverride { get; set; }

    [JsonPropertyName("optionsBranchId")]
    public int OptionsBranchId { get; set; }

    [JsonPropertyName("saleChannelId")]
    public int SaleChannelId { get; set; } = 1;

    [JsonPropertyName("guestNotes")]
    public string? GuestNotes { get; set; }

    [JsonPropertyName("lines")]
    public List<KiotVietOrderOutboxLine> Lines { get; set; } = [];
}

public sealed class KiotVietOrderOutboxLine
{
    [JsonPropertyName("menuItemId")]
    public Guid MenuItemId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = null!;

    /// <summary>Prefer Kiot product code; fallback to <see cref="MenuItem.CatalogKey"/> when no mapping table yet.</summary>
    [JsonPropertyName("productCode")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("lineNote")]
    public string? LineNote { get; set; }
}
