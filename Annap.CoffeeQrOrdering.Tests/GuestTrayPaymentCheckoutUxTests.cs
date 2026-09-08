using System.Text.Json;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestTrayPaymentCheckoutUxTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    [Theory]
    [InlineData("guest-vi.json", "checkout.choosePaymentMethod", "Chọn cách thanh toán")]
    [InlineData("guest-vi.json", "checkout.payAtCounter", "Thanh toán tại quầy")]
    [InlineData("guest-vi.json", "checkout.submitAtCounter", "Gửi đơn — thanh toán tại quầy")]
    [InlineData("guest-vi.json", "checkout.submitForQr", "Gửi đơn — lấy mã QR")]
    [InlineData("guest-vi.json", "checkout.counterPreviewBody", "Sau khi gửi đơn, vui lòng đến quầy để thanh toán. Nhân viên sẽ kiểm tra lại đơn của bạn.")]
    [InlineData("guest-vi.json", "checkout.bankPreviewBody", "Sau khi gửi đơn, mã QR chuyển khoản đúng số tiền sẽ hiện tại đây.")]
    [InlineData("guest-vi.json", "checkout.bankTransferUnavailable", "Chuyển khoản hiện chưa khả dụng. Vui lòng thanh toán tại quầy.")]
    [InlineData("guest-vi.json", "checkout.provisionalBillNote", "Phiếu kiểm đơn này chưa phải hóa đơn đã thanh toán.")]
    public void Guest_i18n_includes_payment_checkout_preview_copy(string file, string dottedKey, string expected)
    {
        var path = Path.Combine(WebRoot, "i18n", file);
        Assert.True(File.Exists(path), $"Missing i18n file: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var parts = dottedKey.Split('.');
        var node = doc.RootElement;
        foreach (var part in parts)
        {
            Assert.True(node.TryGetProperty(part, out node), $"Missing key segment '{part}' in {dottedKey}");
        }

        Assert.Equal(expected, node.GetString());
    }

    [Fact]
    public void Order_tray_dock_renders_payment_preview_before_submit()
    {
        var jsPath = Path.Combine(WebRoot, "js", "order-tray-dock.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("function renderPayment", js, StringComparison.Ordinal);
        Assert.Contains("order-tray-payment-preview", js, StringComparison.Ordinal);
        Assert.Contains("getPaymentSubmitLabel", js, StringComparison.Ordinal);
        Assert.Contains("scrollCheckoutCtaIntoView", js, StringComparison.Ordinal);
        Assert.Contains("function renderCheckoutTray", js, StringComparison.Ordinal);

        var previewStart = js.IndexOf("function renderPayment", StringComparison.Ordinal);
        var previewEnd = js.IndexOf("function scrollCheckoutCtaIntoView", StringComparison.Ordinal);
        Assert.True(previewStart >= 0 && previewEnd > previewStart);
        var previewBlock = js[previewStart..previewEnd];
        Assert.DoesNotContain("fetchTransferQr", previewBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("mountTransferCard", previewBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_tray_dock_fetches_transfer_qr_only_after_bank_submit()
    {
        var jsPath = Path.Combine(WebRoot, "js", "order-tray-dock.js");
        var js = File.ReadAllText(jsPath);

        var submitStart = js.IndexOf("async function submitOrder", StringComparison.Ordinal);
        var submitEnd = js.IndexOf("function annapBindCheckoutControls", StringComparison.Ordinal);
        Assert.True(submitStart >= 0 && submitEnd > submitStart);
        var submitBlock = js[submitStart..submitEnd];
        Assert.DoesNotContain("fetchTransferQr", submitBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("mountTransferCard", submitBlock, StringComparison.Ordinal);

        var submittedStart = js.IndexOf("function renderSubmitted", StringComparison.Ordinal);
        var ownerStart = js.IndexOf("function renderCheckoutTray", StringComparison.Ordinal);
        Assert.True(submittedStart >= 0 && ownerStart > submittedStart);
        var submittedBlock = js[submittedStart..ownerStart];
        Assert.Contains("ensureBankTransferQrMounted(sess)", submittedBlock, StringComparison.Ordinal);
        Assert.Contains("transferHostEl()", js, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_BANK_LOADING", js, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_BANK_READY", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_tray_dock_marks_bank_transfer_unavailable_on_tile()
    {
        var jsPath = Path.Combine(WebRoot, "js", "order-tray-dock.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("order-tray-payment-option--disabled", js, StringComparison.Ordinal);
        Assert.Contains("checkout.bankTransferUnavailable", js, StringComparison.Ordinal);
        Assert.Contains("bankTransferConfigured === false", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_tray_markup_includes_payment_preview_sticky_submit_and_stable_qr_host()
    {
        var cshtmlPath = Path.Combine(PagesRoot, "Shared", "_OrderTrayDock.cshtml");
        var html = File.ReadAllText(cshtmlPath);

        Assert.Contains("order-tray-payment-preview", html, StringComparison.Ordinal);
        Assert.Contains("order-tray-checkout-sticky", html, StringComparison.Ordinal);
        Assert.Contains("checkout.payAtCounterHint", html, StringComparison.Ordinal);
        Assert.Contains("checkout.bankTransferHint", html, StringComparison.Ordinal);
        Assert.Contains("id=\"order-tray-transfer-host\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"order-tray-submitted-panel\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-i18n=\"checkout.reviewOrder\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_tray_dock_clears_submit_in_flight_before_submitted_render()
    {
        var jsPath = Path.Combine(WebRoot, "js", "order-tray-dock.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("TRAY_PAYMENT_FLOW_VERSION", js, StringComparison.Ordinal);
        Assert.Contains("menuOrderSubmitInFlight = false", js, StringComparison.Ordinal);
        Assert.Contains("applySubmitSuccessUi", js, StringComparison.Ordinal);
        Assert.Contains("ensureBankTransferQrMounted", js, StringComparison.Ordinal);
        Assert.Contains("restoreSubmitButtonState", js, StringComparison.Ordinal);

        var applyStart = js.IndexOf("function applySubmitSuccessUi", StringComparison.Ordinal);
        var renderCartIdx = js.IndexOf("renderCart();", applyStart, StringComparison.Ordinal);
        var inFlightClearIdx = js.IndexOf("menuOrderSubmitInFlight = false", applyStart, StringComparison.Ordinal);
        Assert.True(applyStart >= 0 && renderCartIdx > applyStart && inFlightClearIdx > applyStart);
        Assert.True(inFlightClearIdx < renderCartIdx, "Submit in-flight must clear before submitted tray render.");
    }

    [Fact]
    public void Guest_bank_transfer_uses_api_url_helper_and_version_marker()
    {
        var jsPath = Path.Combine(WebRoot, "js", "guest-bank-transfer.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("__annapApiUrl", js, StringComparison.Ordinal);
        Assert.Contains("BANK_TRANSFER_FLOW_VERSION", js, StringComparison.Ordinal);
        Assert.Contains("transfer-qr", js, StringComparison.Ordinal);
    }
}
