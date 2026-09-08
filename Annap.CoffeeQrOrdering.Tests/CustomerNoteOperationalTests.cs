using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class CustomerNoteOperationalTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    private static readonly string PagesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "Pages"));

    [Fact]
    public void Bank_transfer_preview_includes_keep_screen_open_warning()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("order-tray-payment-preview-keepopen", js, StringComparison.Ordinal);
        Assert.Contains("checkout.bankPreviewKeepOpen", js, StringComparison.Ordinal);

        var previewStart = js.IndexOf("function renderPayment", StringComparison.Ordinal);
        var previewEnd = js.IndexOf("function scrollCheckoutCtaIntoView", StringComparison.Ordinal);
        var block = js[previewStart..previewEnd];
        Assert.Contains("PAYMENT_METHOD.BANK", block, StringComparison.Ordinal);
        Assert.Contains("keepOpenEl", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_transfer_qr_card_includes_keep_screen_open_highlight()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "guest-bank-transfer.js"));
        Assert.Contains("guest-bank-transfer__keep-open", js, StringComparison.Ordinal);
        Assert.Contains("checkout.bankTransferKeepOpen", js, StringComparison.Ordinal);
        Assert.Contains("checkout.bankTransferStaffConfirmNote", js, StringComparison.Ordinal);
        Assert.Contains("giữ nguyên màn hình chuyển khoản", js, StringComparison.Ordinal);
        Assert.DoesNotContain("giữ nguyên màn hình này", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Cash_and_card_preview_does_not_surface_bank_keep_open_callout()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        var previewStart = js.IndexOf("function renderPayment", StringComparison.Ordinal);
        var previewEnd = js.IndexOf("function scrollCheckoutCtaIntoView", StringComparison.Ordinal);
        var block = js[previewStart..previewEnd];
        Assert.Contains("checkout.cashPreviewTitle", block, StringComparison.Ordinal);
        Assert.Contains("checkout.cardPreviewTitle", block, StringComparison.Ordinal);
        Assert.Contains("keepOpenEl.classList.add(\"hidden\")", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_tray_does_not_include_order_level_customer_note_field()
    {
        var html = File.ReadAllText(Path.Combine(PagesRoot, "Shared", "_OrderTrayDock.cshtml"));
        Assert.DoesNotContain("order-tray-customer-note", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_tray_dock_submits_per_item_customer_note_in_payload()
    {
        var js = File.ReadAllText(Path.Combine(WebRoot, "js", "order-tray-dock.js"));
        Assert.Contains("item.customerNote = cn", js, StringComparison.Ordinal);
        Assert.DoesNotContain("readCustomerNoteValue", js, StringComparison.Ordinal);
    }
}
