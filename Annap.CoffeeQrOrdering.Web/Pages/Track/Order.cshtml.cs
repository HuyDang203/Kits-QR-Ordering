using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Track;

public sealed class OrderModel : PageModel
{
    public Guid OrderId { get; private set; }

    /// <summary>Guest session credential from the QR handoff URL.</summary>
    public string? TrackToken { get; private set; }

    public void OnGet(Guid orderId)
    {
        OrderId = orderId;
        TrackToken = Request.Query["token"].FirstOrDefault();
        ViewData["Title"] = "Your order";
    }
}
