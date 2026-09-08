namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Development / LAN diagnostics toggles (appsettings:Diagnostics).</summary>
public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    /// <summary>When true in non-Development, treat the app as developer UI for diagnostics overlays and boot flags (use sparingly).</summary>
    public bool DeveloperOverlays { get; set; }

    /// <summary>
    /// When true with developer UI active, guest home skips mood/sommelier/tray/queue boot and only fetches /api/menu for isolation testing.
    /// </summary>
    public bool SlimGuestBoot { get; set; }

    /// <summary>Development: skip sommelier suggest / language fetches on guest home.</summary>
    public bool DisableSommelierBoot { get; set; }

    /// <summary>Development: guest track page skips SignalR (poll only).</summary>
    public bool DisableSignalR { get; set; }

    /// <summary>Development: do not load <c>guest-order-queue.js</c>.</summary>
    public bool DisableGuestQueue { get; set; }

    /// <summary>Development: do not load <c>guest-i18n.js</c> (stub copy is injected).</summary>
    public bool DisableI18n { get; set; }

    /// <summary>Development: skip mood <c>/api/menu</c> catalog boot on home.</summary>
    public bool DisableMoodCatalog { get; set; }

    /// <summary>Development: skip initial cart sheet / banner hydration on boot.</summary>
    public bool DisableCartHydration { get; set; }

    /// <summary>Development: add reduced-motion class + skip non-critical transitions in boot paths.</summary>
    public bool DisableGuestAnimations { get; set; }

    /// <summary>Development: skip IntersectionObserver (menu category spy) and similar observers.</summary>
    public bool DisableGuestObservers { get; set; }

    /// <summary>When true with <c>?bootcheck=1</c>, shows the floating guest boot checklist (Development only).</summary>
    public bool ShowGuestBootChecklist { get; set; }

    /// <summary>Development: show bottom LAN / API debug panel (never in Production).</summary>
    public bool ShowLanDebugOverlay { get; set; }

    /// <summary>Load global error + fetch instrumentation (Development only; off by default).</summary>
    public bool GlobalErrorCapture { get; set; }

    /// <summary>When true, enables EF Core sensitive data logging and detailed errors (very noisy; default false).</summary>
    public bool VerboseSqlLogging { get; set; }
}
