using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class ProductionStorageHealthCheck(
    IWebHostEnvironment environment,
    IOptions<CloudinaryOptions> cloudinaryOptions,
    IOptions<DataProtectionStorageOptions> dataProtectionOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsProduction())
            return Task.FromResult(HealthCheckResult.Healthy("Development storage fallback is active."));

        if (!cloudinaryOptions.Value.IsConfigured)
            return Task.FromResult(HealthCheckResult.Unhealthy("Cloudinary is not configured."));

        var keysPath = dataProtectionOptions.Value.KeysPath?.Trim() ?? "";
        if (!Path.IsPathRooted(keysPath) || !Directory.Exists(keysPath))
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("The Data Protection key directory is unavailable."));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy("Cloudinary and durable Data Protection storage are configured."));
    }
}
