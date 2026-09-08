using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class PaymentWorkflowSchemaStartupExtensions
{
    public static async Task ValidatePaymentWorkflowSchemaAsync(
        WebApplication app,
        CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.PaymentWorkflowSchema");

        if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            return;

        var missing = await PaymentWorkflowSchemaGuard.GetMissingColumnsAsync(db, cancellationToken)
            .ConfigureAwait(false);
        if (missing.Count == 0)
        {
            logger.LogInformation("Order workflow schema check passed.");
            return;
        }

        var detail =
            $"{PaymentWorkflowSchemaGuard.StartupFailureMessage} Missing columns: {string.Join(", ", missing)}. " +
            $"Command: {PaymentWorkflowSchemaGuard.EfUpdateCommand}";

        if (env.IsProduction())
            throw new InvalidOperationException(detail);

        logger.LogError(
            "{Message} Missing columns: {MissingColumns}. Command: {Command}",
            PaymentWorkflowSchemaGuard.StartupFailureMessage,
            string.Join(", ", missing),
            PaymentWorkflowSchemaGuard.EfUpdateCommand);
    }
}
