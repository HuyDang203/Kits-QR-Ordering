using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCodeValue { get; set; } = 500;

    /// <summary>Guest-facing errors use bilingual i18n; staff/admin errors stay Vietnamese-only.</summary>
    public bool GuestFacing { get; private set; } = true;

    public string TitleKey { get; set; } = "errors.genericTitle";

    public string LedeKey { get; set; } = "errors.genericLede";

    public void OnGet(int? statusCode = null)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        StatusCodeValue = statusCode is > 0 ? statusCode.Value : Response.StatusCode;
        if (StatusCodeValue is < 400 or > 599)
            StatusCodeValue = 500;

        Response.StatusCode = StatusCodeValue;

        var path = HttpContext.Request.Path.Value ?? "";
        GuestFacing = !path.StartsWith("/staff", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        (TitleKey, LedeKey) = StatusCodeValue switch
        {
            404 => ("errors.404Title", "errors.404Lede"),
            403 => ("errors.403Title", "errors.403Lede"),
            503 => ("errors.503Title", "errors.503Lede"),
            _ => ("errors.genericTitle", "errors.genericLede")
        };
    }
}
