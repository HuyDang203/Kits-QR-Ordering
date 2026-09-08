using QRCoder;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Shared PNG data-URI QR builder (table demo QR, optional local generation).</summary>
internal static class QrCodeDataUriBuilder
{
    public static string FromText(string payload, int pixelsPerModule = 20)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(pixelsPerModule);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}
