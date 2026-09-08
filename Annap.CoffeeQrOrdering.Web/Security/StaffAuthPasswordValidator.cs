namespace Annap.CoffeeQrOrdering.Web.Security;

/// <summary>Shared staff password rules for login and production startup guard.</summary>
public static class StaffAuthPasswordValidator
{
    public const int MinProductionLength = 12;

    public static readonly string[] WeakPasswords =
    [
        "changeme",
        "change-this-staff-password",
        "change-this-admin-password",
        "change-this-checkout-password",
        "change-this-barista-password",
        "change-this-local-or-production-password",
        "password",
        "password123",
        "admin",
        "admin123",
        "123456",
        "123456789012",
        "staff",
        "staffpassword",
        "annap",
        "annapcoffee",
        "host",
        "welcome",
        "qwertyuiop12",
        "checkout-dev",
        "barista-dev"
    ];

    public static void ValidateProductionPassword(string settingName, string? password)
    {
        var pwd = password?.Trim() ?? "";
        if (pwd.Length < MinProductionLength)
        {
            throw new InvalidOperationException(
                $"Production: StaffAuth:{settingName} must be at least {MinProductionLength} characters. " +
                $"Set StaffAuth__{settingName} via environment.");
        }

        foreach (var weak in WeakPasswords)
        {
            if (pwd.Equals(weak, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Production: StaffAuth:{settingName} is a known weak or development default. " +
                    $"Choose a unique password for {settingName}.");
            }
        }
    }
}
