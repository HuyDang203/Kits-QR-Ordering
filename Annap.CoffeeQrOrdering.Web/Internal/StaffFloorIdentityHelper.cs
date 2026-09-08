using System.Security.Claims;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Security;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Resolves staff floor header identity from auth claims.</summary>
public sealed record StaffFloorIdentityVm(string Headline, string? Subline, string RoleLabel);

internal static class StaffFloorIdentityHelper
{
    public static StaffFloorIdentityVm? Resolve(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        if (user.IsInRole(StaffRoleNames.Admin))
            return new StaffFloorIdentityVm("Đang đăng nhập: Quản lý", null, "Quản lý");

        var displayName = user.FindFirst(StaffClaimTypes.DisplayName)?.Value?.Trim();
        var username = user.FindFirst(StaffClaimTypes.Username)?.Value?.Trim();
        var accountRole = user.FindFirst(StaffClaimTypes.AccountRole)?.Value;
        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(username))
        {
            var roleLabel = StaffAccountRoles.LabelVi(accountRole);
            return new StaffFloorIdentityVm(
                $"Đang đăng nhập: {displayName}",
                $"@{username} · {roleLabel}",
                roleLabel);
        }

        if (user.IsInRole(StaffRoleNames.Checkout))
            return new StaffFloorIdentityVm("Đang đăng nhập: Nhân viên kiểm đơn", null, "Kiểm đơn");

        if (user.IsInRole(StaffRoleNames.Barista))
            return new StaffFloorIdentityVm("Đang đăng nhập: Pha chế", "Pha chế chung", "Pha chế");

        return new StaffFloorIdentityVm("Đang đăng nhập", null, "");
    }
}
