namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Guest ordering UX toggles (appsettings:Ordering).
/// </summary>
public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    /// <summary>
    /// When true (recommended for cafés), seated QR landing surfaces a primary server-rendered link to the menu/tray
    /// before optional experiential flows. Does not remove flows — only changes prominence.
    /// </summary>
    public bool PrioritizeDirectMenu { get; set; } = true;
}
