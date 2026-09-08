using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class PaymentWorkflowSchemaHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missing = await PaymentWorkflowSchemaGuard.GetMissingColumnsAsync(db, cancellationToken)
            .ConfigureAwait(false);

        if (missing.Count == 0)
            return HealthCheckResult.Healthy("Order workflow schema is applied.");

        return HealthCheckResult.Unhealthy(
            $"{PaymentWorkflowSchemaGuard.StartupFailureMessage} Missing: {string.Join(", ", missing)}. " +
            $"Run: {PaymentWorkflowSchemaGuard.EfUpdateCommand}");
    }
}
