using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Fail-fast checks before accepting production traffic (secrets, unsafe defaults).
/// </summary>
public static class ProductionStartupGuard
{
    public static void Validate(IHostEnvironment env, IConfiguration configuration)
    {
        if (!env.IsProduction()) return;

        var staff = configuration.GetSection(StaffAuthOptions.SectionName).Get<StaffAuthOptions>()
            ?? new StaffAuthOptions();

        StaffAuthPasswordValidator.ValidateProductionPassword(nameof(StaffAuthOptions.Password), staff.Password);
        StaffAuthPasswordValidator.ValidateProductionPassword(
            nameof(StaffAuthOptions.CheckoutPassword),
            staff.CheckoutPassword);
        StaffAuthPasswordValidator.ValidateProductionPassword(
            nameof(StaffAuthOptions.BaristaPassword),
            staff.BaristaPassword);

        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Production: ConnectionStrings:DefaultConnection is required.");

        if (conn.Contains("Password=annap_local_dev", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: local PostgreSQL password detected. Set POSTGRES_PASSWORD to a deployment-specific value.");
        }

        if (conn.Contains("Password=change-this-local-or-production-password", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: placeholder PostgreSQL password detected. Set POSTGRES_PASSWORD before starting production.");
        }

        if (conn.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: default PostgreSQL password detected. Set POSTGRES_PASSWORD to a deployment-specific value.");
        }

        var cloudinary = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>()
            ?? new CloudinaryOptions();
        if (!cloudinary.IsConfigured)
        {
            throw new InvalidOperationException(
                "Production: Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret are required.");
        }

        if (IsPlaceholder(cloudinary.CloudName)
            || IsPlaceholder(cloudinary.ApiKey)
            || IsPlaceholder(cloudinary.ApiSecret))
        {
            throw new InvalidOperationException(
                "Production: Cloudinary credentials look like placeholders. Set real Cloudinary__* values.");
        }

        var dataProtection = configuration
            .GetSection(DataProtectionStorageOptions.SectionName)
            .Get<DataProtectionStorageOptions>() ?? new DataProtectionStorageOptions();
        if (string.IsNullOrWhiteSpace(dataProtection.KeysPath)
            || !Path.IsPathRooted(dataProtection.KeysPath))
        {
            throw new InvalidOperationException(
                "Production: DataProtection:KeysPath must be an absolute path on a Render persistent disk.");
        }

        var devWebhookEnabled = configuration.GetValue(
            "BankTransfer:Webhook:DevWebhookEnabled",
            false);
        if (devWebhookEnabled)
        {
            throw new InvalidOperationException(
                "Production: the development bank-transfer webhook must remain disabled.");
        }

        if (PublicBaseUrlRules.ConnectionStringUsesLoopbackHost(conn))
        {
            throw new InvalidOperationException(
                "Production: ConnectionStrings:DefaultConnection must not use localhost/127.0.0.1. Use the private database hostname.");
        }

        var publicBase = configuration["AppUrl:PublicBaseUrl"];
        if (!PublicBaseUrlRules.TryNormalizeAbsoluteHttpUrl(publicBase, out var normalizedBase, out var baseError))
        {
            throw new InvalidOperationException(
                "Production: AppUrl:PublicBaseUrl is required (absolute https URL of the live site, not localhost). "
                + (baseError ?? ""));
        }

        if (!Uri.TryCreate(normalizedBase, UriKind.Absolute, out var publicUri)
            || !string.Equals(publicUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production: AppUrl:PublicBaseUrl must use https (e.g. https://annapcoffee.io.vn).");
        }
    }

    private static bool IsPlaceholder(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            return true;

        return trimmed.Equals("your-cloud-name", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("your-api-key", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("your-api-secret", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("your-", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("change-this-", StringComparison.OrdinalIgnoreCase);
    }
}
