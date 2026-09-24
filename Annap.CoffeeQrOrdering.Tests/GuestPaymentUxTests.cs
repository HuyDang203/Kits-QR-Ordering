namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestPaymentUxTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    [Fact]
    public void Tray_polls_guest_track_json_api_not_html_track_page()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("fetchGuestOrderStatus", js, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", js, StringComparison.Ordinal);
        Assert.Contains("cache: \"no-store\"", js, StringComparison.Ordinal);

        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTrayChrome", StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("fetchGuestOrderStatus(sess)", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("buildTrackHref(sess)", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("fingerprintChanged", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_i18n_bundles_use_cache_busting_and_no_store()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-i18n.js"));
        Assert.Contains("I18N_BUNDLE_REV", js, StringComparison.Ordinal);
        Assert.Contains("cache: \"no-store\"", js, StringComparison.Ordinal);
        Assert.Contains("/i18n/", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_track_api_url_helper_exists()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-interaction-contract.js"));
        Assert.Contains("buildGuestTrackApiUrl", js, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_detection_helper_recognizes_paid_preparing_payload()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("function isOrderPaidForGuest", js, StringComparison.Ordinal);
        Assert.Contains("paid_preparing", js, StringComparison.Ordinal);
        Assert.Contains("paidAtUtc", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_qr_uses_updated_keep_screen_open_copy()
    {
        var vi = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-vi.json"));
        Assert.Contains("giữ nguyên màn hình chuyển khoản", vi, StringComparison.Ordinal);
        Assert.DoesNotContain("giữ nguyên màn hình này sau khi chuyển khoản", vi, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-bank-transfer.js"));
        Assert.Contains("checkout.bankTransferKeepOpen", js, StringComparison.Ordinal);
        Assert.Contains("giữ nguyên màn hình chuyển khoản", js, StringComparison.Ordinal);
        Assert.DoesNotContain("giữ nguyên màn hình này", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_preview_uses_updated_keep_screen_open_copy()
    {
        var vi = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-vi.json"));
        Assert.Contains(
            "Sau khi chuyển khoản, vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra.",
            vi,
            StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("checkout.bankPreviewKeepOpen", js, StringComparison.Ordinal);
        Assert.Contains("\"bankTransferPendingBodyShort\": \"Quét mã QR để thanh toán.\"", vi, StringComparison.Ordinal);
    }

    [Fact]
    public void Cash_and_card_preview_does_not_use_bank_transfer_keep_open_copy()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function renderPayment", StringComparison.Ordinal);
        var end = js.IndexOf("function scrollCheckoutCtaIntoView", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("checkout.cashPreviewTitle", block, StringComparison.Ordinal);
        Assert.Contains("checkout.cardPreviewTitle", block, StringComparison.Ordinal);

        var cardStart = block.IndexOf("PAYMENT_METHOD.CARD", StringComparison.Ordinal);
        var elseStart = block.IndexOf("checkout.cashPreviewTitle", StringComparison.Ordinal);
        Assert.True(cardStart >= 0 && elseStart > cardStart);
        var counterBlock = block[cardStart..elseStart];
        Assert.Contains("keepOpenEl.classList.add(\"hidden\")", counterBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("bankPreviewKeepOpen", counterBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Tray_polls_pending_payment_and_celebrates_once_on_paid_transition()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("TRAY_STATUS_POLL_MS", js, StringComparison.Ordinal);
        Assert.Contains("startTrayStatusPolling", js, StringComparison.Ordinal);
        Assert.Contains("celebratedPaidOrderIds", js, StringComparison.Ordinal);
        Assert.Contains("handlePaymentConfirmedCelebration", js, StringComparison.Ordinal);
        Assert.Contains("showPaymentSuccessCelebration", js, StringComparison.Ordinal);
        Assert.Contains("setTrayOpen(false)", js, StringComparison.Ordinal);

        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTrayChrome", StringComparison.Ordinal);
        var refreshBlock = js[refreshStart..refreshEnd];
        Assert.Contains("wasPending && isPaid", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("celebratedPaidOrderIds.has", refreshBlock, StringComparison.Ordinal);
        Assert.Contains("resolveSubmittedCounterState(sess.paymentMethod)", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("traySubmittedStatus = TRAY_STATE.SUBMITTED_PENDING;", refreshBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("renderCart()", refreshBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_pending_state_keeps_qr_loading_and_fallback_render()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function ensureBankTransferQrMounted", StringComparison.Ordinal);
        var end = js.IndexOf("function applySubmitSuccessUi", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("checkout.transferQrLoading", block, StringComparison.Ordinal);
        Assert.Contains("/api/track/orders/", block, StringComparison.Ordinal);
        Assert.Contains("track.transferQr", block, StringComparison.Ordinal);
        Assert.Contains("transferHostEl()", block, StringComparison.Ordinal);
        Assert.Contains("transferUnavailableInTray", js, StringComparison.Ordinal);
        Assert.Contains("transferRetry", js, StringComparison.Ordinal);
        Assert.Contains("window.__annapBankTransferDebug", js, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySubmitSuccessUi_transitions_checkout_before_clearCart()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function applySubmitSuccessUi", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = js.IndexOf("\n        function ", start + 1, StringComparison.Ordinal);
        var block = end > start ? js[start..end] : js[start..];
        var setUi = block.IndexOf("setCheckoutUi(nextUi", StringComparison.Ordinal);
        var clearCart = block.IndexOf("GuestInteractionContract.clearCart()", StringComparison.Ordinal);
        Assert.True(setUi >= 0, "setCheckoutUi(nextUi) missing from applySubmitSuccessUi");
        Assert.True(clearCart >= 0, "clearCart missing from applySubmitSuccessUi");
        Assert.True(setUi < clearCart, "checkout UI must transition before clearCart to keep single render owner");
    }

    [Fact]
    public void Bank_transfer_inline_fallback_renders_qr_image_when_url_exists()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function renderInlineTransferFallback", StringComparison.Ordinal);
        var end = js.IndexOf("function renderTransferFatalFallback", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("guest-bank-transfer__qr", block, StringComparison.Ordinal);
        Assert.Contains("loading=\"eager\"", block, StringComparison.Ordinal);
        Assert.Contains("qrImageLoadState", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_bank_transfer_view_hides_detail_rows_and_copy_buttons()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "guest-tray-submitted.css"));
        Assert.Contains(".guest-bank-transfer--tray .guest-bank-transfer__meta", css, StringComparison.Ordinal);
        Assert.Contains(".guest-bank-transfer--tray .guest-bank-transfer__actions", css, StringComparison.Ordinal);
        Assert.Contains("display: none;", css, StringComparison.Ordinal);
        Assert.Contains(".guest-bank-transfer--tray.guest-bank-transfer--fallback .guest-bank-transfer__fallback-details", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_render_uses_stable_host_and_single_owner()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("function renderCheckoutTray", js, StringComparison.Ordinal);
        Assert.Contains("function renderSubmitted", js, StringComparison.Ordinal);
        Assert.Contains("function updateCTA", js, StringComparison.Ordinal);
        Assert.Contains("CHECKOUT_UI", js, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_BANK_LOADING", js, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_BANK_READY", js, StringComparison.Ordinal);
        Assert.Contains("order-tray-transfer-host", js, StringComparison.Ordinal);
        Assert.DoesNotContain("function renderSubmittedTraySheet", js, StringComparison.Ordinal);
        Assert.DoesNotContain("data-i18n=\"checkout.reviewOrder\"", js, StringComparison.Ordinal);

        var start = js.IndexOf("function renderSubmitted", StringComparison.Ordinal);
        var end = js.IndexOf("function renderCheckoutTray", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("ensureBankTransferQrMounted(sess)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("el.innerHTML = `<div class=\"${cardClass}\"", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Payment_success_celebration_respects_reduced_motion()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function runTrayConfettiBurst", StringComparison.Ordinal);
        var end = js.IndexOf("function showPaymentSuccessCelebration", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("reducedMotionTray()", block, StringComparison.Ordinal);

        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "guest-tray-submitted.css"));
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-confetti", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_tray_state_hides_bank_qr_mount_path()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("isBankCheckoutUi(checkoutUi)", js, StringComparison.Ordinal);
        Assert.Contains("CHECKOUT_UI.PAID", js, StringComparison.Ordinal);
        Assert.Contains("CHECKOUT_UI.COMPLETED", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkout_cta_not_owned_by_i18n_applyDom()
    {
        var markup = File.ReadAllText(
            Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "Annap.CoffeeQrOrdering.Web",
                    "Pages",
                    "Shared",
                    "_OrderTrayDock.cshtml")));
        Assert.Contains("id=\"menuSubmitBtn\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-i18n=\"checkout.reviewOrder\"", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"order-tray-transfer-host\"", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"order-tray-submitted-panel\"", markup, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.DoesNotContain("LuxuryI18n.applyDom()", js, StringComparison.Ordinal);
        Assert.Contains("function updateCTA", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_poll_does_not_recreate_cart_dom()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function startTrayStatusPolling", StringComparison.Ordinal);
        var end = js.IndexOf("function dismissPaymentSuccessCelebration", StringComparison.Ordinal);
        var block = js[start..end];
        Assert.Contains("fingerprintChanged", block, StringComparison.Ordinal);
        Assert.Contains("renderCheckoutTray()", block, StringComparison.Ordinal);
        Assert.DoesNotContain("renderCart()", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Submitted_panel_includes_table_identity_badge_element()
    {
        var markup = File.ReadAllText(
            Path.Combine(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages")),
                "Shared",
                "_OrderTrayDock.cshtml"));
        Assert.Contains("id=\"order-tray-submitted-table\"", markup, StringComparison.Ordinal);
        Assert.Contains("order-tray-submitted-card__table", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_tray_submitted_styles_define_table_badge()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "guest-tray-submitted.css"));
        Assert.Contains(".order-tray-submitted-card__table", css, StringComparison.Ordinal);
        Assert.Contains(".order-tray-root--submitted-paid .order-tray-submitted-card__table", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Submitted_panel_renders_table_badge_from_track_then_guest_ctx()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("function resolveSubmittedTableBadgeText", js, StringComparison.Ordinal);
        Assert.Contains("lastGuestTrackData", js, StringComparison.Ordinal);
        Assert.Contains("GUEST_CTX.label", js, StringComparison.Ordinal);
        Assert.Contains("tableCode", js, StringComparison.Ordinal);
        Assert.Contains("cart.tableMetaShort", js, StringComparison.Ordinal);

        var renderSubmitted = js.IndexOf("function renderSubmitted", StringComparison.Ordinal);
        var renderCheckout = js.IndexOf("function renderCheckoutTray", StringComparison.Ordinal);
        Assert.True(renderSubmitted >= 0 && renderCheckout > renderSubmitted);
        var submittedBlock = js[renderSubmitted..renderCheckout];
        Assert.Contains("order-tray-submitted-table", submittedBlock, StringComparison.Ordinal);
        Assert.Contains("resolveSubmittedTableBadgeText()", submittedBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Track_poll_caches_payload_for_submitted_panel_table_badge()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var end = js.IndexOf("function updateTrayChrome", StringComparison.Ordinal);
        var block = js[start..end];
        // Caching logic must run when a successful track response arrives.
        Assert.Contains("lastGuestTrackData = data", block, StringComparison.Ordinal);
    }

    [Fact]
    public void HandlePaymentConfirmedCelebration_registers_cleanup_on_dismiss()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function handlePaymentConfirmedCelebration", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = js.IndexOf("\n        function ", start + 1, StringComparison.Ordinal);
        var block = end > start ? js[start..end] : js[start..];

        // Required cleanup primitives must run on celebration dismiss.
        Assert.Contains("removeGuestOrderSession()", block, StringComparison.Ordinal);
        Assert.Contains("stopTrayStatusPolling()", block, StringComparison.Ordinal);
        Assert.Contains("traySubmittedStatus = null", block, StringComparison.Ordinal);
        Assert.Contains("trayKnownPendingPayment = null", block, StringComparison.Ordinal);
        Assert.Contains("trayStatusFingerprint = \"\"", block, StringComparison.Ordinal);
        Assert.Contains("setCheckoutUi(CHECKOUT_UI.EDITING)", block, StringComparison.Ordinal);
        Assert.Contains("cleanupFired", block, StringComparison.Ordinal);

        // The dismiss hook must be wired through showPaymentSuccessCelebration.
        var showStart = js.IndexOf("function showPaymentSuccessCelebration", StringComparison.Ordinal);
        Assert.True(showStart >= 0);
        var showEnd = js.IndexOf("\n        function ", showStart + 1, StringComparison.Ordinal);
        var showBlock = showEnd > showStart ? js[showStart..showEnd] : js[showStart..];
        Assert.Contains("opts.onDismiss", showBlock, StringComparison.Ordinal);
        Assert.Contains("onDismiss()", showBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_celebration_does_not_clear_unrelated_cart_items()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("function handlePaymentConfirmedCelebration", StringComparison.Ordinal);
        var end = js.IndexOf("function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var block = js[start..end];
        // Cleanup must not call clearCart — only remove the submitted-order session
        // so the customer can immediately start a new order without losing state.
        Assert.Contains("removeGuestOrderSession()", block, StringComparison.Ordinal);
        Assert.DoesNotContain("GuestInteractionContract.clearCart()", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Celebration_replay_guard_preserved_for_paid_order_ids()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        // celebratedPaidOrderIds must still exist and be consulted on the next poll.
        Assert.Contains("celebratedPaidOrderIds", js, StringComparison.Ordinal);
        var refreshStart = js.IndexOf("async function refreshSubmittedTrayStatus", StringComparison.Ordinal);
        var refreshEnd = js.IndexOf("function updateTrayChrome", StringComparison.Ordinal);
        var block = js[refreshStart..refreshEnd];
        Assert.Contains("celebratedPaidOrderIds.has(orderKey)", block, StringComparison.Ordinal);
        Assert.Contains("celebratedPaidOrderIds.add(orderKey)", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_payment_cta_is_bound_and_renders_only_while_pending()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        // New state variables exist.
        Assert.Contains("pendingPaymentMethodChange", js, StringComparison.Ordinal);
        Assert.Contains("paymentMethodChangeInflight", js, StringComparison.Ordinal);
        Assert.Contains("submitPaymentMethodChange", js, StringComparison.Ordinal);
        // New endpoint is referenced.
        Assert.Contains("/api/orders/${encodeURIComponent(sess.orderId)}/payment-method", js, StringComparison.Ordinal);
        Assert.Contains("/payment-method", js, StringComparison.Ordinal);
        // The CTA click handler is bound in annapBindCheckoutControls and pre-selects current method.
        Assert.Contains("order-tray-submitted-change-payment", js, StringComparison.Ordinal);
        Assert.Contains("paymentMethodChangePreselect = pm", js, StringComparison.Ordinal);
        Assert.Contains("pendingPaymentMethodChange = true", js, StringComparison.Ordinal);
        // submitOrder routes to the dedicated branch when pendingPaymentMethodChange is set.
        var submitStart = js.IndexOf("async function submitOrder", StringComparison.Ordinal);
        Assert.True(submitStart >= 0);
        var submitEnd = js.IndexOf("\n        async function ", submitStart + 1, StringComparison.Ordinal);
        var submitBlock = submitEnd > submitStart ? js[submitStart..submitEnd] : js[submitStart..];
        Assert.Contains("pendingPaymentMethodChange", submitBlock, StringComparison.Ordinal);
        Assert.Contains("submitPaymentMethodChange()", submitBlock, StringComparison.Ordinal);
        // Reconciliation on 409 / payment_method_locked is wired up.
        Assert.Contains("payment_method_locked", js, StringComparison.Ordinal);
        Assert.Contains("refreshSubmittedTrayStatus()", js, StringComparison.Ordinal);
        // In-flight guard exists.
        Assert.Contains("paymentMethodChangeInflight = true", js, StringComparison.Ordinal);
        Assert.Contains("paymentMethodChangeInflight = false", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_payment_back_button_returns_to_submitted_not_review()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        // The back button handler must short-circuit when the user is in change-payment mode.
        var backStart = js.IndexOf("backBtn.addEventListener", StringComparison.Ordinal);
        Assert.True(backStart >= 0);
        var backEnd = js.IndexOf("\n        }\n", backStart, StringComparison.Ordinal);
        var backBlock = backEnd > backStart ? js[backStart..backEnd] : js[backStart..];
        Assert.Contains("pendingPaymentMethodChange", backBlock, StringComparison.Ordinal);
        Assert.Contains("CHECKOUT_UI.SUBMITTED", backBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_payment_submit_does_not_call_post_orders_or_clearCart()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var start = js.IndexOf("async function submitPaymentMethodChange", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = js.IndexOf("\n        function ", start + 1, StringComparison.Ordinal);
        var block = end > start ? js[start..end] : js[start..];
        // Branch B must not call POST /api/orders, must not clearCart, must not create new order.
        Assert.DoesNotContain("\"/api/orders\"", block, StringComparison.Ordinal);
        Assert.DoesNotContain("GuestInteractionContract.clearCart()", block, StringComparison.Ordinal);
        Assert.DoesNotContain("menuOrderIdempotencyKey", block, StringComparison.Ordinal);
        // Must POST to the dedicated endpoint and preserve the orderId + token.
        Assert.Contains("/payment-method", block, StringComparison.Ordinal);
        Assert.Contains("token: sess.token", block, StringComparison.Ordinal);
        // Session write preserves orderId/token/venueTableId/submittedAt/status, only updates paymentMethod.
        Assert.Contains("writeGuestOrderSession", block, StringComparison.Ordinal);
        Assert.Contains("orderId: sess.orderId", block, StringComparison.Ordinal);
        Assert.Contains("token: sess.token", block, StringComparison.Ordinal);
        Assert.Contains("venueTableId: sess.venueTableId", block, StringComparison.Ordinal);
        Assert.Contains("submittedAt: sess.submittedAt", block, StringComparison.Ordinal);
        Assert.Contains("status: sess.status", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Change_payment_markup_and_i18n_keys_exist()
    {
        var markup = File.ReadAllText(
            Path.Combine(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages")),
                "Shared",
                "_OrderTrayDock.cshtml"));
        Assert.Contains("id=\"order-tray-submitted-change-payment\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-i18n=\"submitted.changePaymentCta\"", markup, StringComparison.Ordinal);

        var vi = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-vi.json"));
        Assert.Contains("\"submitted\"", vi, StringComparison.Ordinal);
        Assert.Contains("\"changePaymentCta\": \"Đổi phương thức thanh toán\"", vi, StringComparison.Ordinal);

        var en = File.ReadAllText(Path.Combine(WebRoot, "i18n", "guest-en.json"));
        Assert.Contains("\"changePaymentCta\": \"Change payment method\"", en, StringComparison.Ordinal);
    }
}
