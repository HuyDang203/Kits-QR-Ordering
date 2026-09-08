using System.Security.Claims;
using Annap.CoffeeQrOrdering.Web.Security;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class StaffPaymentConfirmerHelper
{
    public static (string DisplayName, Guid? AccountId) ResolveConfirmer(ClaimsPrincipal user)
    {
        Guid? accountId = null;
        var accountIdRaw = user.FindFirst(StaffClaimTypes.AccountId)?.Value;
        if (Guid.TryParse(accountIdRaw, out var parsedId))
            accountId = parsedId;

        var displayName = user.FindFirst(StaffClaimTypes.DisplayName)?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
            return (displayName, accountId);

        var username = user.FindFirst(StaffClaimTypes.Username)?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(username))
            return (username, accountId);

        if (user.IsInRole(StaffRoleNames.Admin))
            return ("Quản lý", null);

        if (user.IsInRole(StaffRoleNames.Checkout))
            return ("Nhân viên kiểm đơn", null);

        var name = user.Identity?.Name?.Trim();
        return string.IsNullOrWhiteSpace(name) ? ("Nhân viên", null) : (name, null);
    }
}
