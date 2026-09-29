using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Static / JS-shape verification for the server-reconciliation-first behavior
/// of the Guest Order Session. No Docker required — reads the JS source and
/// asserts the boot order, terminal-state cleanup helper, and venueTableId
/// guard are wired correctly.
/// </summary>
public sealed class GuestSessionReconciliationJsTests
{
    private static string WebRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    /// <summary>
    /// Extracts the body of a top-level function declaration starting at
    /// <paramref name="fnStart"/> by counting balanced braces from the first
    /// '{' after the function signature to its matching '}'.
    /// </summary>
    private static string ExtractFunctionBody(string js, int fnStart)
    {
        var openBrace = js.IndexOf('{', fnStart);
        Assert.True(openBrace > 0);
        var depth = 0;
        for (var i = openBrace; i < js.Length; i++)
        {
            if (js[i] == '{') depth++;
            else if (js[i] == '}')
            {
                depth--;
                if (depth == 0) return js[openBrace..(i + 1)];
            }
        }
        Assert.Fail("Unbalanced function body");
        return "";
    }

    private static string ExtractNextFunctionBody(string js, string functionName)
    {
        var sigIdx = js.IndexOf("function " + functionName, StringComparison.Ordinal);
        Assert.True(sigIdx > 0, "Function " + functionName + " not found");
        return ExtractFunctionBody(js, sigIdx);
    }

    [Fact]
    public void Boot_reconciles_against_server_before_first_render()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));

        // bootMenuTray must await server reconciliation BEFORE the first
        // checkoutUi / tray UI is committed.
        var bootStart = js.IndexOf("function bootMenuTray", StringComparison.Ordinal);
        Assert.True(bootStart > 0, "bootMenuTray not found");

        var initialRender = js.IndexOf("renderCheckoutTray();", bootStart, StringComparison.Ordinal);
        var reconcileCall = js.IndexOf("refreshSubmittedTrayStatus()", bootStart, StringComparison.Ordinal);
        Assert.True(initialRender > 0);
        Assert.True(reconcileCall > 0);
        // Initial render must appear BEFORE the reconcile call so that the
        // initial paint is the safe EDITING default.
        Assert.True(initialRender < reconcileCall,
            "Initial renderCheckoutTray() must happen before refreshSubmittedTrayStatus() during boot so stale session cannot paint first.");

        // The block must start with EDITING so the initial paint is safe.
        var section = js.Substring(bootStart, reconcileCall - bootStart);
        Assert.Contains("checkoutUi = CHECKOUT_UI.EDITING;", section, StringComparison.Ordinal);
        Assert.Contains("readSubmittedSession()", section, StringComparison.Ordinal);
        Assert.Contains("hasActiveSess", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_completed_response_clears_guest_order_session()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));

        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        Assert.True(refreshStart > 0);
        var refreshEnd = js.IndexOf("function updateTrayChrome", refreshStart, StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("data.isComplete", refreshBlock, StringComparison.Ordinal);
        // The new behavior must invoke the canonical reset helper so the
        // local Guest Order Session is REMOVED, not just re-flagged as
        // completed. The helper itself drops the session via
        // removeGuestOrderSession().
        Assert.Contains("resetGuestOrderSessionUi(\"server_completed\")", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("setCheckoutUi(CHECKOUT_UI.EDITING)", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("terminalCleared: true", refreshBlock, StringComparison.Ordinal);

        // The reset helper must exist and call removeGuestOrderSession().
        var helperStart = js.IndexOf("function resetGuestOrderSessionUi", StringComparison.Ordinal);
        Assert.True(helperStart > 0);
        Assert.Contains("removeGuestOrderSession()", ExtractFunctionBody(js, helperStart), StringComparison.Ordinal);
    }

    [Fact]
    public void Server_cancelled_response_clears_guest_order_session()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTrayChrome", refreshStart, StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("data.isCancelled", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("resetGuestOrderSessionUi(\"server_cancelled\")", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_404_clears_guest_order_session()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTrayChrome", refreshStart, StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("res.status === 404", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("resetGuestOrderSessionUi(\"server_404\")", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_helper_drops_all_transient_state()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var block = ExtractNextFunctionBody(js, "resetGuestOrderSessionUi");
        Assert.NotEmpty(block);

        // Every transient state variable bound to the submitted order must be
        // cleared so the Guest can immediately start a fresh order.
        Assert.Contains("stopTrayStatusPolling()", block, StringComparison.Ordinal);
        Assert.Contains("removeGuestOrderSession()", block, StringComparison.Ordinal);
        Assert.Contains("traySubmittedStatus = null", block, StringComparison.Ordinal);
        Assert.Contains("trayKnownPendingPayment = null", block, StringComparison.Ordinal);
        Assert.Contains("trayStatusFingerprint = \"\"", block, StringComparison.Ordinal);
        Assert.Contains("bankTransferQrCache = null", block, StringComparison.Ordinal);
        Assert.Contains("lastGuestTrackData = null", block, StringComparison.Ordinal);
        Assert.Contains("pendingPaymentMethodChange = false", block, StringComparison.Ordinal);
        Assert.Contains("paymentMethodChangePreselect = null", block, StringComparison.Ordinal);
        Assert.Contains("paymentMethodChangeInflight = false", block, StringComparison.Ordinal);
        Assert.Contains("linesToCartMap", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_guest_order_session_validates_venue_table_id()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));
        var block = ExtractNextFunctionBody(js, "readGuestOrderSession");
        Assert.NotEmpty(block);

        Assert.Contains("storedVt", block, StringComparison.Ordinal);
        Assert.Contains("removeGuestOrderSession()", block, StringComparison.Ordinal);
        Assert.Contains("storedVt !== _venueId", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_binds_table_id_before_reading_session()
    {
        // The Menu page binds the page context BEFORE reading the session,
        // so a stored session whose venueTableId does not match the current
        // table is dropped automatically. Verify the call order in the JS.
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var bootStart = js.IndexOf("function bootMenuTray", StringComparison.Ordinal);
        var section = js.Substring(bootStart);

        // syncCartKey (which calls bindPageContext → sets _venueId) appears
        // BEFORE readSubmittedSession so the venueTableId guard is effective.
        var syncIdx = section.IndexOf("syncCartKey()", StringComparison.Ordinal);
        var readIdx = section.IndexOf("readSubmittedSession()", StringComparison.Ordinal);
        Assert.True(syncIdx >= 0);
        Assert.True(readIdx >= 0);
        Assert.True(syncIdx < readIdx);
    }

    [Fact]
    public void Original_orders_remain_intact_after_reconciliation()
    {
        // Reconciliation ONLY clears the client-side Guest Order Session
        // pointer in localStorage. It MUST NOT delete or mutate the Order
        // record in the database.
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var contractJs = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));

        var resetBlock = ExtractNextFunctionBody(js, "resetGuestOrderSessionUi");
        Assert.NotEmpty(resetBlock);

        Assert.DoesNotContain("fetch(", resetBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/orders", resetBlock, StringComparison.OrdinalIgnoreCase);
        // The 'delete' JS keyword would imply a destructive DB call. The
        // helper uses removeItem + Map.set(null) only — never the DB.
        Assert.DoesNotContain("celebratedPaidOrderIds.delete", resetBlock, StringComparison.Ordinal);
        // The helper must call removeGuestOrderSession() (which itself
        // executes localStorage.removeItem) — that is the only allowed drop.
        Assert.Contains("removeGuestOrderSession()", resetBlock, StringComparison.Ordinal);
        // The contract function actually removes from localStorage.
        var contractBlock = ExtractNextFunctionBody(contractJs, "removeGuestOrderSession");
        Assert.Contains("localStorage.removeItem", contractBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Reconciliation_does_not_call_payment_endpoint()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var resetBlock = ExtractNextFunctionBody(js, "resetGuestOrderSessionUi");
        Assert.DoesNotContain("mark-paid", resetBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payment-method", resetBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/orders/", resetBlock, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Active_session_preserves_localstorage_pointer_through_reload()
    {
        // Sanity: writeGuestOrderSession is still the canonical write path.
        // Reconciliation must NOT clobber an active session.
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));
        Assert.Contains("function writeGuestOrderSession", js, StringComparison.Ordinal);
        Assert.Contains("localStorage.setItem(orderSessionKey", js, StringComparison.Ordinal);
        var refreshStart = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"))
            .IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        Assert.True(refreshStart > 0);
    }

    [Fact]
    public void Boot_reconcile_catch_handles_stale_completed_locally()
    {
        // If the network fails during boot reconciliation, the catch path
        // must still drop a local COMPLETED session so the Guest is never
        // stuck on "Đơn đã hoàn thành" because of an unreachable server.
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var bootStart = js.IndexOf("function bootMenuTray", StringComparison.Ordinal);
        var section = js.Substring(bootStart);
        Assert.Contains("network_fallback_stale_completed", section, StringComparison.Ordinal);
        Assert.Contains("resetGuestOrderSessionUi", section, StringComparison.Ordinal);
    }
}
