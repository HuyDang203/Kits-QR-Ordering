using System.Security.Claims;
using Annap.CoffeeQrOrdering.Web.Security;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class StaffBaristaActorHelper
{
    public static (string DisplayName, Guid? AccountId) ResolveActor(ClaimsPrincipal user)
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

        if (user.IsInRole(StaffRoleNames.Barista))
            return ("Pha chế", null);

        var name = user.Identity?.Name?.Trim();
        return string.IsNullOrWhiteSpace(name) ? ("Không rõ", null) : (name, null);
    }
}
