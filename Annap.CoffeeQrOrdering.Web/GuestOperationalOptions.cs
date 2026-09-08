namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Café operational tuning for seated QR guests (not dev diagnostics).
/// </summary>
public sealed class GuestOperationalOptions
{
    public const string SectionName = "GuestOperational";

    /// <summary>
    /// When true, the seated guest homepage shows a primary server-rendered link to the menu
    /// before optional experiences. Ordering works without experiential JS.
    /// </summary>
    public bool MenuFirstArrival { get; set; } = true;

    /// <summary>
    /// When true, seated guest homepage applies <c>ge-root--calm</c> to shorten heavy CSS animations
    /// (better on low-end phones and in-app browsers). Does not remove optional flows.
    /// </summary>
    public bool CalmArrivalAnimations { get; set; } = true;
}
