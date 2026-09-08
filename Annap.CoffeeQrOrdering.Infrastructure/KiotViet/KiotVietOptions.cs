namespace Annap.CoffeeQrOrdering.Infrastructure.KiotViet;

public sealed class KiotVietOptions
{
    public const string SectionName = "KiotViet";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Retailer { get; set; } = "";
    public int BranchId { get; set; }

    /// <summary>API host for business calls (excluding token endpoint).</summary>
    public string BaseUrl { get; set; } = "https://public.kiotapi.com";

    public string AuthUrl { get; set; } = "https://id.kiotviet.vn/connect/token";

    public string Scope { get; set; } = "PublicApi.Access";

    /// <summary>Relative path appended to retailer base route (leading slashes trimmed).</summary>
    public string OrdersPath { get; set; } = "orders";

    public string TablesPath { get; set; } = "tables";

    public int OrderDispatchIntervalSeconds { get; set; } = 5;

    public int InventoryPollIntervalMinutes { get; set; } = 5;

    public int ProductSyncIntervalMinutes { get; set; } = 60;

    /// <summary>Max failed delivery attempts before dead-letter.</summary>
    public int MaxDispatchRetries { get; set; } = 5;

    /// <summary>Max rows claimed per dispatch tick.</summary>
    public int DispatchBatchSize { get; set; } = 20;

    /// <summary>Concurrent HTTP pushes per batch.</summary>
    public int DispatchMaxConcurrent { get; set; } = 4;

    /// <summary>How long Processing may stick before reclaimed as Pending.</summary>
    public int StuckProcessingSeconds { get; set; } = 120;

    public string WebhookSecret { get; set; } = "";

    /// <summary>When false (default): outbox rows are queued but outbound HTTP calls are suppressed.</summary>
    public bool IsEnabled { get; set; }

    internal Uri ResolvedBaseUri() =>
        Uri.TryCreate(BaseUrl.TrimEnd('/'), UriKind.Absolute, out var u) ? u : new Uri("https://public.kiotapi.com/");
}
