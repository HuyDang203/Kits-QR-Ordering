using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class PaymentWorkflowSchemaGuardTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public void IsMissingPaymentColumnException_detects_BillNumber_42703()
    {
        var ex = new PostgresException("column o.BillNumber does not exist", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "42703");
        Assert.True(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex));
    }

    [Fact]
    public void IsMissingPaymentColumnException_detects_PreparedQuantity_42703()
    {
        var ex = new PostgresException("column oi.PreparedQuantity does not exist", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "42703");
        Assert.True(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex));
    }

    [Fact]
    public void IsMissingPaymentColumnException_detects_order_items_table_reference()
    {
        var ex = new PostgresException("column \"PreparedBy\" of relation \"order_items\" does not exist", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "42703");
        Assert.True(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex));
    }

    [Fact]
    public void IsMissingPaymentColumnException_ignores_unrelated_postgres_errors()
    {
        var ex = new PostgresException("duplicate key", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "23505");
        Assert.False(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex));
    }

    [Fact]
    public void IsMissingPaymentColumnException_detects_wrapped_exception()
    {
        var inner = new PostgresException("column \"PaidAtUtc\" does not exist", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "42703");
        var outer = new InvalidOperationException("query failed", inner);
        Assert.True(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(outer));
    }

    [Fact]
    public async Task GetMissingColumnsAsync_returns_empty_when_migration_applied()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missing = await PaymentWorkflowSchemaGuard.GetMissingColumnsAsync(db);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task Payment_workflow_health_check_is_healthy_after_migration()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var health = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var report = await health.CheckHealthAsync(reg => reg.Name == "payment_workflow_schema");
        var entry = Assert.Single(report.Entries);
        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
    }
}
