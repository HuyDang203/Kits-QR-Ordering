using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Application.Integration;

/// <summary>
/// Immutable JSON snapshot stored in <see cref="Domain.Entities.KiotViet.KiotVietOutboxMessage.Payload"/>
/// at order submission time. Contains every field the dispatch worker needs — no future DB joins required.
/// </summary>
public sealed class KiotVietOrderPayload
{
    /// <summary>Increment when the shape changes to allow versioned deserialization in the worker.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("venueTableId")]
    public Guid VenueTableId { get; init; }

    [JsonPropertyName("tableCode")]
    public string TableCode { get; init; } = null!;

    /// <summary>KiotViet table ID at snapshot time; null when table is not yet mapped in POS.</summary>
    [JsonPropertyName("kiotVietTableId")]
    public string? KiotVietTableId { get; init; }

    /// <summary>Per-table branch override; null means the worker uses the global KiotViet:BranchId.</summary>
    [JsonPropertyName("kiotVietBranchId")]
    public int? KiotVietBranchId { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("items")]
    public List<KiotVietOrderPayloadLine> Items { get; init; } = [];
}

public sealed class KiotVietOrderPayloadLine
{
    [JsonPropertyName("menuItemId")]
    public Guid MenuItemId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>ANNAP catalog key; used as the default KiotViet product code when no explicit mapping exists.</summary>
    [JsonPropertyName("catalogKey")]
    public string? CatalogKey { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    /// <summary>Price locked at submission time — immune to post-submission price changes.</summary>
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
