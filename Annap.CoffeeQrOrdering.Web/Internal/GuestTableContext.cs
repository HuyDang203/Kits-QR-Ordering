namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Guest table / QR context for ordering vs browse-only.</summary>
public enum GuestTableContextState
{
  /// <summary>No table; public browse may be allowed.</summary>
  PublicBrowse,

  /// <summary>Active venue table resolved — ordering allowed.</summary>
  SeatedValid,

  /// <summary>QR path (<c>/table</c> or <c>/t</c>) did not resolve to an active table.</summary>
  QrScanInvalid,

  /// <summary><c>?vt=</c> handoff did not resolve to an active table.</summary>
  HandoffInvalid
}

public static class GuestTableContext
{
  public static GuestTableContextState Resolve(bool hasValidTable, bool qrScanAttempted, bool handoffAttempted)
  {
    if (hasValidTable)
      return GuestTableContextState.SeatedValid;
    if (qrScanAttempted)
      return GuestTableContextState.QrScanInvalid;
    if (handoffAttempted)
      return GuestTableContextState.HandoffInvalid;
    return GuestTableContextState.PublicBrowse;
  }

  public static bool AllowsOrderSubmit(GuestTableContextState state)
    => state == GuestTableContextState.SeatedValid;
}
