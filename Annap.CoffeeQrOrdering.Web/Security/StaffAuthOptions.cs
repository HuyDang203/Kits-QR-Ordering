namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>
/// Shared staff/admin sign-in. Development may use the default password;
/// production must set strong values for <see cref="Password"/>, <see cref="CheckoutPassword"/>,
/// and <see cref="BaristaPassword"/> (≥12 characters, not known weak/dev defaults).
/// <see cref="ProductionStartupGuard"/> fails fast when requirements are not met.
/// </summary>
public sealed class StaffAuthOptions
{
    public const string SectionName = "StaffAuth";

    /// <summary>Sign-in name for the staff back room.</summary>
    public string UserName { get; set; } = "host";

    /// <summary>Local-dev default only — override via <c>StaffAuth__Password</c> in production.</summary>
    public string Password { get; set; } = "ChangeMe";

    /// <summary>Checkout device password. Falls back to <see cref="Password"/> when empty in development.</summary>
    public string CheckoutPassword { get; set; } = "";

    /// <summary>Barista/prep device password. Falls back to <see cref="Password"/> when empty in development.</summary>
    public string BaristaPassword { get; set; } = "";
}
