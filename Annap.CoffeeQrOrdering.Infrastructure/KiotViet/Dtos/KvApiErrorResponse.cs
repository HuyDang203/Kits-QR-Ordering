using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Dtos;

internal sealed class KvApiErrorResponse
{
    /// <summary>KiotViet wraps errors in responseStatus when using ServiceStack serialization.</summary>
    [JsonPropertyName("responseStatus")]
    public KvResponseStatus? ResponseStatus { get; init; }

    /// <summary>Top-level errorCode on some endpoints.</summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

internal sealed class KvResponseStatus
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
