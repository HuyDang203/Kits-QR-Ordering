using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ProductionStartupGuardTests
{
    private static void Validate(
        string environment,
        string staffPassword,
        string? checkoutPassword = null,
        string? baristaPassword = null,
        string? connectionString = null,
        string? cloudName = "test-cloud",
        string? cloudApiKey = "test-api-key",
        string? cloudApiSecret = "test-api-secret",
        string? dataProtectionKeysPath = null,
        bool devWebhookEnabled = false,
        string? publicBaseUrl = "https://coffee.example.com")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StaffAuth:UserName"] = "host",
                ["StaffAuth:Password"] = staffPassword,
                ["StaffAuth:CheckoutPassword"] = checkoutPassword ?? "CheckoutFloor-2026-Secure",
                ["StaffAuth:BaristaPassword"] = baristaPassword ?? "BaristaFloor-2026-Secure",
                ["ConnectionStrings:DefaultConnection"] = connectionString
                    ?? "Host=postgres;Database=test;Username=postgres;Password=deployment-secret-32;SSL Mode=Require",
                ["Cloudinary:CloudName"] = cloudName,
                ["Cloudinary:ApiKey"] = cloudApiKey,
                ["Cloudinary:ApiSecret"] = cloudApiSecret,
                ["DataProtection:KeysPath"] = dataProtectionKeysPath
                    ?? Path.Combine(Path.GetTempPath(), "annap-test-dp-keys"),
                ["DataProtection:ApplicationName"] = "Annap.Tests",
                ["BankTransfer:Webhook:DevWebhookEnabled"] = devWebhookEnabled.ToString(),
                ["AppUrl:PublicBaseUrl"] = publicBaseUrl
            })
            .Build();

        var env = new HostEnvironment { EnvironmentName = environment };
        ProductionStartupGuard.Validate(env, config);
    }

    [Fact]
    public void Development_allows_default_ChangeMe_password()
    {
        var ex = Record.Exception(() => Validate(Environments.Development, "ChangeMe", "", ""));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("ChangeMe")]
    [InlineData("changeme")]
    [InlineData("password")]
    [InlineData("admin")]
    [InlineData("short")]
    public void Production_rejects_weak_or_short_staff_password(string password)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Validate(Environments.Production, password));
        Assert.Contains("StaffAuth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_accepts_strong_unique_staff_passwords()
    {
        var ex = Record.Exception(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                "CheckoutFloor-2026-Secure",
                "BaristaFloor-2026-Secure"));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("checkout-dev", "BaristaFloor-2026-Secure")]
    [InlineData("BaristaFloor-2026-Secure", "barista-dev")]
    public void Production_rejects_dev_default_role_passwords(string checkoutPassword, string baristaPassword)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                checkoutPassword,
                baristaPassword));

        Assert.Contains("StaffAuth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_empty_checkout_password()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                "",
                "BaristaFloor-2026-Secure"));

        Assert.Contains("CheckoutPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_local_postgres_password_in_connection_string()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                connectionString: "Host=postgres;Database=test;Username=postgres;Password=annap_local_dev"));

        Assert.Contains("PostgreSQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_requires_Cloudinary()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(Environments.Production, "CafeFloor-2026-Secure", cloudName: ""));

        Assert.Contains("Cloudinary", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_placeholder_Cloudinary_credentials()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                cloudName: "your-cloud-name",
                cloudApiKey: "your-api-key",
                cloudApiSecret: "your-api-secret"));

        Assert.Contains("Cloudinary", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_requires_durable_DataProtection_path()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                dataProtectionKeysPath: ""));

        Assert.Contains("DataProtection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_development_bank_webhook()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                devWebhookEnabled: true));

        Assert.Contains("webhook", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("checkout-dev")]
    [InlineData("barista-dev")]
    [InlineData("change-this-admin-password")]
    [InlineData("change-this-checkout-password")]
    [InlineData("change-this-barista-password")]
    public void StaffAuthPasswordValidator_rejects_known_dev_defaults(string password)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StaffAuthPasswordValidator.ValidateProductionPassword("TestPassword", password));
        Assert.True(
            ex.Message.Contains("weak", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("at least", StringComparison.OrdinalIgnoreCase),
            ex.Message);
    }

    [Fact]
    public void Production_rejects_env_example_staff_password_placeholders()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "change-this-admin-password",
                "change-this-checkout-password",
                "change-this-barista-password"));

        Assert.Contains("StaffAuth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_requires_public_base_url()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(Environments.Production, "CafeFloor-2026-Secure", publicBaseUrl: ""));

        Assert.Contains("PublicBaseUrl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_localhost_public_base_url()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(Environments.Production, "CafeFloor-2026-Secure", publicBaseUrl: "http://localhost:8080"));

        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_http_public_base_url()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(Environments.Production, "CafeFloor-2026-Secure", publicBaseUrl: "http://annapcoffee.io.vn"));

        Assert.Contains("https", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_loopback_database_host()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Validate(
                Environments.Production,
                "CafeFloor-2026-Secure",
                connectionString: "Host=127.0.0.1;Database=test;Username=postgres;Password=deployment-secret-32"));

        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Annap.CoffeeQrOrdering.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
