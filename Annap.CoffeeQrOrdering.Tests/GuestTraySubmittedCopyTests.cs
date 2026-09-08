using System.Text.Json;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestTraySubmittedCopyTests
{
    private static readonly string WebRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Annap.CoffeeQrOrdering.Web", "wwwroot"));

    [Theory]
    [InlineData("guest-vi.json", "menuTray.chipSubmittedTitle", "Đơn đã được gửi")]
    [InlineData("guest-vi.json", "menuTray.chipSubmittedBody", "Nhân viên sẽ đến kiểm tra lại đơn và hỗ trợ thanh toán.")]
    [InlineData("guest-vi.json", "menuTray.chipTrackCta", "Theo dõi đơn")]
    [InlineData("guest-en.json", "menuTray.chipSubmittedTitle", "Order sent")]
    [InlineData("guest-en.json", "menuTray.chipSubmittedBody", "Staff will come to confirm your order and help with payment.")]
    [InlineData("guest-en.json", "menuTray.chipTrackCta", "Track order")]
    public void Guest_i18n_includes_submitted_tray_copy(string file, string dottedKey, string expected)
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
    public void Order_tray_dock_uses_submitted_state_machine()
    {
        var jsPath = Path.Combine(WebRoot, "js", "order-tray-dock.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("submittedPendingPayment", js, StringComparison.Ordinal);
        Assert.Contains("order-tray-root--submitted", js, StringComparison.Ordinal);
        Assert.Contains("function renderSubmitted", js, StringComparison.Ordinal);
        Assert.Contains("function renderCheckoutTray", js, StringComparison.Ordinal);
        Assert.Contains("buildGuestTrackUrl", js, StringComparison.Ordinal);
        Assert.DoesNotContain("function renderSubmittedTraySheet", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Guest_interaction_contract_stores_submitted_session_metadata()
    {
        var jsPath = Path.Combine(WebRoot, "js", "guest-interaction-contract.js");
        var js = File.ReadAllText(jsPath);

        Assert.Contains("submittedAt", js, StringComparison.Ordinal);
        Assert.Contains("buildGuestTrackUrl", js, StringComparison.Ordinal);
        Assert.Contains("venueTableId", js, StringComparison.Ordinal);
    }
}
