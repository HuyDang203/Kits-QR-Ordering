using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Dtos;

internal sealed class KvCreateOrderRequest
{
    [JsonPropertyName("branchId")]
    public int BranchId { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("saleChannelId")]
    public int? SaleChannelId { get; init; }

    [JsonPropertyName("orderDetails")]
    public List<KvCreateOrderDetailRequest> OrderDetails { get; init; } = [];
}

internal sealed class KvCreateOrderDetailRequest
{
    /// <summary>KiotViet product ID; null when using productCode lookup instead.</summary>
    [JsonPropertyName("productId")]
    public long? ProductId { get; init; }

    /// <summary>Product barcode/code; used as fallback when no explicit productId mapping.</summary>
    [JsonPropertyName("productCode")]
    public string? ProductCode { get; init; }

    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = null!;

    /// <summary>KiotViet quantity is double, not int.</summary>
    [JsonPropertyName("quantity")]
    public double Quantity { get; init; }

    /// <summary>Unit price; field name in KiotViet API is "price", not "unitPrice".</summary>
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}
