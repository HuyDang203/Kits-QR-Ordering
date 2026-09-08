namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>Custom claims for individual staff accounts.</summary>
public static class StaffClaimTypes
{
    public const string AccountId = "annap:staff_account_id";
    public const string DisplayName = "annap:staff_display_name";
    public const string Username = "annap:staff_username";
    public const string CanCloseShift = "annap:can_close_shift";
    public const string AccountRole = "annap:staff_account_role";
}

/// <summary>Future shift-close permission placeholder (Phase 2).</summary>
public static class StaffPermissionNames
{
    public const string CanCloseShift = "CanCloseShift";
}
