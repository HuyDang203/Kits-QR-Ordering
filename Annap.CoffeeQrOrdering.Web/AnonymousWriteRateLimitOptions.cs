namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Fixed-window limits for anonymous write APIs (per remote IP partition).</summary>
public sealed class AnonymousWriteRateLimitOptions
{
    public const string SectionName = "RateLimiting:AnonymousWrites";

    /// <summary>Max anonymous POSTs per IP per window (orders, sommelier, discovery, chat).</summary>
    public int PermitLimit { get; set; } = 48;

    /// <summary>Window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
