using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Dtos;

internal sealed class KvCreateOrderResponse
{
    /// <summary>KiotViet order ID; a long, stored as string in our outbox.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("branchId")]
    public int BranchId { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("total")]
    public decimal Total { get; init; }
}
