namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ItemCustomerNoteStaticTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    [Fact]
    public void Guest_tray_cart_line_key_uses_id_when_menu_item_id_missing()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function cartLineKey", StringComparison.Ordinal);
        var end = js.IndexOf("const cartItems = new Map", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("l.menuItemId", block, StringComparison.Ordinal);
        Assert.Contains("l.id", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_tray_submit_flushes_dom_notes_and_reloads_cart_before_payload()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("function syncOpenNoteTextareasFromDom", js, StringComparison.Ordinal);
        Assert.Contains("tray-line-note-input", js, StringComparison.Ordinal);

        var flushStart = js.IndexOf("function flushAllNoteDraftsForSubmit", StringComparison.Ordinal);
        var flushEnd = js.IndexOf("function refreshNotePillForKey", StringComparison.Ordinal);
        var flushBlock = js[flushStart..flushEnd];
        Assert.Contains("syncOpenNoteTextareasFromDom", flushBlock, StringComparison.Ordinal);
        Assert.Contains("linesToCartMap(GuestInteractionContract.getCartLines())", flushBlock, StringComparison.Ordinal);

        var submitStart = js.IndexOf("async function submitOrder", StringComparison.Ordinal);
        var flushInSubmit = js.IndexOf("flushAllNoteDraftsForSubmit();", submitStart, StringComparison.Ordinal);
        Assert.True(submitStart >= 0 && flushInSubmit > submitStart, "submitOrder must flush note drafts before building payload.");
        Assert.Contains("item.customerNote = cn", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_tray_has_per_item_note_action_not_order_level_note()
    {
        var html = File.ReadAllText(Path.Combine(PagesRoot, "Shared", "_OrderTrayDock.cshtml"));
        Assert.DoesNotContain("order-tray-customer-note", html, StringComparison.Ordinal);
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("tray-line-note-toggle", js, StringComparison.Ordinal);
        Assert.Contains("item.customerNote = cn", js, StringComparison.Ordinal);
        Assert.Contains("setLineCustomerNote", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Staff_board_js_renders_item_customer_note_with_encoding()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));

        var noteHelperStart = js.IndexOf("function buildItemCustomerNoteHtml", StringComparison.Ordinal);
        var noteHelperEnd = js.IndexOf("function buildItemLine", StringComparison.Ordinal);
        var noteHelper = js[noteHelperStart..noteHelperEnd];
        Assert.Contains("it.customerNote", noteHelper, StringComparison.Ordinal);
        Assert.Contains("escapeHtml(trimmed)", noteHelper, StringComparison.Ordinal);
        Assert.Contains("Ghi chú:", noteHelper, StringComparison.Ordinal);
        Assert.Contains("staff-order-note--item", noteHelper, StringComparison.Ordinal);

        var lineStart = js.IndexOf("function buildItemLine", StringComparison.Ordinal);
        var lineEnd = js.IndexOf("function buildCardActions", StringComparison.Ordinal);
        var lineBlock = js[lineStart..lineEnd];
        Assert.Contains("buildItemCustomerNoteHtml(it)", lineBlock, StringComparison.Ordinal);

        var notePos = lineBlock.IndexOf("${note}", StringComparison.Ordinal);
        var prepPos = lineBlock.IndexOf("${prepControls}", StringComparison.Ordinal);
        Assert.True(notePos >= 0 && prepPos > notePos, "Item note must render before prep controls.");
    }

    [Fact]
    public void Staff_board_card_does_not_render_detached_guest_notes_block()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        var start = js.IndexOf("function buildCardHtml", StringComparison.Ordinal);
        var end = js.IndexOf("function renderEmptyNode", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.DoesNotContain("guestNotes", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Ghi chú món", block, StringComparison.Ordinal);
        Assert.DoesNotContain("staff-order-note--ticket", block, StringComparison.Ordinal);
        Assert.DoesNotContain("${itemNotes}", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Staff_board_item_note_html_is_encoded_not_raw()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        var start = js.IndexOf("function buildItemCustomerNoteHtml", StringComparison.Ordinal);
        var end = js.IndexOf("function buildItemLine", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("escapeHtml(trimmed)", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Bill_modal_js_renders_item_customer_note()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        var start = js.IndexOf("function renderBillHtml", StringComparison.Ordinal);
        var end = js.IndexOf("function openBillSheet", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("staff-bill-receipt__line-note", block, StringComparison.Ordinal);
        Assert.Contains("it.customerNote", block, StringComparison.Ordinal);
        Assert.Contains("escapeHtml(String(itemNoteRaw).trim())", block, StringComparison.Ordinal);

        var notePos = block.IndexOf("${itemNoteLine}", StringComparison.Ordinal);
        var metaPos = block.IndexOf("staff-bill-receipt__line-meta", StringComparison.Ordinal);
        Assert.True(notePos >= 0 && metaPos > notePos, "Bill item note must appear under item name.");
    }

    [Fact]
    public void Staff_board_api_maps_item_customer_note()
    {
        var cs = File.ReadAllText(Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "Annap.CoffeeQrOrdering.Web", "Extensions", "OrderWorkflowEndpoints.cs")));
        Assert.Contains("customerNote = i.CustomerNote", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void Staff_board_card_keeps_payment_and_confirmer_lines()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        var start = js.IndexOf("function buildCardHtml", StringComparison.Ordinal);
        var end = js.IndexOf("function renderEmptyNode", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("staff-order-card__payment-confirmed", block, StringComparison.Ordinal);
        Assert.Contains("Xác nhận bởi", block, StringComparison.Ordinal);
        Assert.Contains("buildPrepControls", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_item_customer_note_does_not_render_note_block()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        var start = js.IndexOf("function buildItemCustomerNoteHtml", StringComparison.Ordinal);
        var end = js.IndexOf("function buildItemLine", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("if (!trimmed) return \"\";", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Note_input_uses_silent_persistence_without_full_cart_emit()
    {
        var trayJs = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var gicJs = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));

        var inputStart = trayJs.IndexOf("ta.addEventListener(\"input\"", StringComparison.Ordinal);
        Assert.True(inputStart > 0);
        var inputBlock = trayJs[inputStart..(inputStart + 420)];
        Assert.Contains("persistLineCustomerNoteDraft", inputBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("renderCart()", inputBlock, StringComparison.Ordinal);

        Assert.Contains("function isNoteEditorFocused", trayJs, StringComparison.Ordinal);
        Assert.Contains("if (isNoteEditorFocused()) return;", trayJs, StringComparison.Ordinal);
        Assert.Contains("flushAllNoteDraftsForSubmit", trayJs, StringComparison.Ordinal);

        Assert.Contains("options.silent === true", gicJs, StringComparison.Ordinal);
        Assert.Contains("if (!silent) emitCartUpdated();", gicJs, StringComparison.Ordinal);
    }

    [Fact]
    public void Note_textarea_stops_event_propagation_for_mobile_typing()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("stopNoteEditorEvent", js, StringComparison.Ordinal);
        Assert.Contains("\"touchstart\"", js, StringComparison.Ordinal);
        Assert.Contains("\"pointerdown\"", js, StringComparison.Ordinal);
    }
}
