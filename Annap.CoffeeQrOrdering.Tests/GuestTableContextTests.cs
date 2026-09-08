using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestTableContextTests
{
    [Theory]
    [InlineData(true, false, false, GuestTableContextState.SeatedValid)]
    [InlineData(true, true, true, GuestTableContextState.SeatedValid)]
    public void Resolve_valid_table_always_seated(bool hasTable, bool qrAttempted, bool handoffAttempted, GuestTableContextState expected)
    {
        Assert.Equal(expected, GuestTableContext.Resolve(hasTable, qrAttempted, handoffAttempted));
    }

    [Fact]
    public void Resolve_qr_scan_without_table_is_invalid()
    {
        Assert.Equal(GuestTableContextState.QrScanInvalid, GuestTableContext.Resolve(false, true, false));
    }

    [Fact]
    public void Resolve_handoff_without_table_is_invalid()
    {
        Assert.Equal(GuestTableContextState.HandoffInvalid, GuestTableContext.Resolve(false, false, true));
    }

    [Fact]
    public void Resolve_public_browse_without_signals()
    {
        Assert.Equal(GuestTableContextState.PublicBrowse, GuestTableContext.Resolve(false, false, false));
    }

    [Fact]
    public void Allows_order_submit_only_when_seated_valid()
    {
        Assert.True(GuestTableContext.AllowsOrderSubmit(GuestTableContextState.SeatedValid));
        Assert.False(GuestTableContext.AllowsOrderSubmit(GuestTableContextState.PublicBrowse));
        Assert.False(GuestTableContext.AllowsOrderSubmit(GuestTableContextState.QrScanInvalid));
        Assert.False(GuestTableContext.AllowsOrderSubmit(GuestTableContextState.HandoffInvalid));
    }
}
