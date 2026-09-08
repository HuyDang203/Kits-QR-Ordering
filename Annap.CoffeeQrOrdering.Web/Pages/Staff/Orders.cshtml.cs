using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff;

[Authorize(Policy = "StaffFloor")]
public sealed class OrdersModel : PageModel
{
    public bool CanCloseShift { get; private set; }

    /// <summary>Mobile tab key: submitted | paid | completed.</summary>
    public string DefaultMobileTab { get; private set; } = "submitted";

    public string? MobileIdentityLine { get; private set; }

    public void OnGet()
    {
        ViewData["Title"] = "Sàn phục vụ";
        ViewData["StaffSubtitle"] = "Đơn → Đã thanh toán → Hoàn thành";
        ViewData["StaffBodyClass"] = "staff-orders-page";

        CanCloseShift = StaffAuthorizationHelper.CanCloseShift(User);

        var canMarkPaid = StaffAuthorizationHelper.CanMarkPaid(User);
        var canComplete = StaffAuthorizationHelper.CanComplete(User);
        DefaultMobileTab = canComplete && !canMarkPaid ? "paid" : "submitted";

        var identity = StaffFloorIdentityHelper.Resolve(User);
        if (identity is not null)
        {
            MobileIdentityLine = identity.Subline is { Length: > 0 } sub
                ? sub.TrimStart('@')
                : identity.RoleLabel;
        }
    }
}
