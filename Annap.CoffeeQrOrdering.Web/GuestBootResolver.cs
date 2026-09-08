namespace Annap.CoffeeQrOrdering.Web;

public static class GuestBootResolver
{
    /// <summary>
    /// If <paramref name="safeQuery"/> is set, safe ladder wins for subsystem toggles.
    /// Otherwise, when <paramref name="useDevelopmentDiagnostics"/> is true, <see cref="DiagnosticsOptions"/> flags apply.
    /// </summary>
    public static GuestBootConfig Resolve(bool useDevelopmentDiagnostics, DiagnosticsOptions diagnostics, string? safeQuery, string? bootcheck)
    {
        var c = new GuestBootConfig();
        var safe = (safeQuery ?? "").Trim();

        if (!string.IsNullOrEmpty(safe))
            ApplySafeQuery(c, safe);
        else if (useDevelopmentDiagnostics)
            CopyFromDiagnostics(c, diagnostics);

        // Checklist is opt-in (explicit query or Diagnostics flag) only when developer UI is active — never for ?safe= alone.
        c.ShowBootChecklist =
            useDevelopmentDiagnostics
            && (
                string.Equals((bootcheck ?? "").Trim(), "1", StringComparison.Ordinal)
                || diagnostics.ShowGuestBootChecklist);

        c.SafeQuery = safe;
        return c;
    }

    private static void CopyFromDiagnostics(GuestBootConfig c, DiagnosticsOptions d)
    {
        c.DisableSommelierBoot = d.DisableSommelierBoot;
        c.DisableSignalR = d.DisableSignalR;
        c.DisableGuestQueue = d.DisableGuestQueue;
        c.DisableI18n = d.DisableI18n;
        c.DisableMoodCatalog = d.DisableMoodCatalog;
        c.DisableCartHydration = d.DisableCartHydration;
        c.DisableGuestAnimations = d.DisableGuestAnimations;
        c.DisableGuestObservers = d.DisableGuestObservers;
    }

    /// <summary>Everything off except minimal shell; tokens add subsystems back.</summary>
    private static void ApplySafeQuery(GuestBootConfig c, string raw)
    {
        var s = raw.Trim();
        if (string.IsNullOrEmpty(s))
            return;

        if (s.Equals("full-no-sommelier", StringComparison.OrdinalIgnoreCase))
        {
            // Full guest minus sommelier API/boot path only (client still runs mood grid if enabled).
            c.DisableSommelierBoot = true;
            return;
        }

        if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMinimalGuest(c);
            return;
        }

        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ApplyMinimalGuest(c);

        if (parts.Contains("i18n"))
            c.DisableI18n = false;
        if (parts.Contains("cart") || parts.Contains("hydration"))
            c.DisableCartHydration = false;
        if (parts.Contains("mood"))
            c.DisableMoodCatalog = false;
        if (parts.Contains("sommelier"))
            c.DisableSommelierBoot = false;
        if (parts.Contains("queue"))
            c.DisableGuestQueue = false;
        if (parts.Contains("signalr"))
            c.DisableSignalR = false;
        if (parts.Contains("animations"))
            c.DisableGuestAnimations = false;
        if (parts.Contains("observers"))
            c.DisableGuestObservers = false;
    }

    /// <summary>safe=1 / safe=menu baseline: menu fetch + plain interaction; no advanced systems.</summary>
    private static void ApplyMinimalGuest(GuestBootConfig c)
    {
        c.DisableSommelierBoot = true;
        c.DisableSignalR = true;
        c.DisableGuestQueue = true;
        c.DisableI18n = true;
        c.DisableMoodCatalog = true;
        c.DisableCartHydration = true;
        c.DisableGuestAnimations = true;
        c.DisableGuestObservers = true;
    }
}
