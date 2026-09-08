namespace Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;

/// <summary>Append-only integration audit trail (order push, future product/inventory sync).</summary>
public sealed class KiotVietSyncLog
{
    public long Id { get; set; }

    public string SyncKind { get; set; } = null!;

    public bool IsSuccess { get; set; }

    public string? ReferenceId { get; set; }

    public string? KiotVietReference { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? FailureReason { get; set; }

    public long DurationMs { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Optional free-form detail (never secrets).</summary>
    public string? Detail { get; set; }
}
