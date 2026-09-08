namespace Annap.CoffeeQrOrdering.Tests;

public sealed class PaymentMethodSplitStaticTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    [Fact]
    public void Guest_tray_exposes_cash_card_and_bank_transfer_payment_options()
    {
        var html = File.ReadAllText(Path.Combine(PagesRoot, "Shared", "_OrderTrayDock.cshtml"));
        Assert.Contains("data-payment-method=\"Cash\"", html, StringComparison.Ordinal);
        Assert.Contains("data-payment-method=\"Card\"", html, StringComparison.Ordinal);
        Assert.Contains("data-payment-method=\"BankTransfer\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-payment-method=\"CashOrCardAtCounter\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Cash", "checkout.cashPreviewTitle", "checkout.submitCash")]
    [InlineData("Card", "checkout.cardPreviewTitle", "checkout.submitCard")]
    public void Guest_tray_js_has_cash_and_card_counter_copy(string method, string titleKey, string ctaKey)
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        if (method == "Cash")
            Assert.Contains("PAYMENT_METHOD.CASH", js, StringComparison.Ordinal);
        else if (method == "Card")
            Assert.Contains("PAYMENT_METHOD.CARD", js, StringComparison.Ordinal);
        Assert.Contains(titleKey, js, StringComparison.Ordinal);
        Assert.Contains(ctaKey, js, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_tray_js_bank_transfer_still_surfaces_qr_flow()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("PAYMENT_METHOD.BANK", js, StringComparison.Ordinal);
        Assert.Contains("checkout.submitForQr", js, StringComparison.Ordinal);
        Assert.Contains("ensureBankTransferQrMounted", js, StringComparison.Ordinal);
        Assert.Contains("transferHostEl", js, StringComparison.Ordinal);
        Assert.Contains("PAYMENT_BANK_READY", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Staff_board_js_encodes_payment_method_labels()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "staff-orders-board.js"));
        Assert.Contains("resolvePaymentMethodLabel", js, StringComparison.Ordinal);
        Assert.Contains("pendingPaymentBadgeVi", js, StringComparison.Ordinal);
        Assert.Contains("Thanh toán:", js, StringComparison.Ordinal);
    }
}
