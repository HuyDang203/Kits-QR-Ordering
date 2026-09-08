using System.Reflection;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Build-time schema-drift protection: mapped OrderItem columns must stay in SchemaGuard,
/// and the Temperature migration must be discoverable by EF.
/// </summary>
public sealed class OrderWorkflowSchemaContractTests
{
    public const string OrderItemTemperatureMigrationId = "20260805120000_AddOrderItemTemperature";

    [Fact]
    public void Every_OrderItem_mapped_column_is_in_PaymentWorkflowSchemaGuard()
    {
        using var db = CreateModelContext();
        var entity = db.Model.FindEntityType(typeof(OrderItem));
        Assert.NotNull(entity);

        var tableName = entity!.GetTableName();
        Assert.False(string.IsNullOrWhiteSpace(tableName));
        var store = StoreObjectIdentifier.Table(tableName!, entity.GetSchema());

        var mappedColumns = entity.GetProperties()
            .Select(p => p.GetColumnName(store))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(mappedColumns);

        var guarded = PaymentWorkflowSchemaGuard.RequiredOrderItemColumns
            .ToHashSet(StringComparer.Ordinal);

        var missingFromGuard = mappedColumns.Where(c => !guarded.Contains(c!)).ToArray();
        Assert.True(
            missingFromGuard.Length == 0,
            "OrderItem mapped columns missing from PaymentWorkflowSchemaGuard.RequiredOrderItemColumns: "
            + string.Join(", ", missingFromGuard));

        Assert.Contains("Temperature", PaymentWorkflowSchemaGuard.RequiredOrderItemColumns, StringComparer.Ordinal);
    }

    [Fact]
    public void OrderItemTemperature_migration_is_discoverable_by_ef()
    {
        using var db = CreateModelContext();
        var defined = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);

        Assert.Contains(OrderItemTemperatureMigrationId, defined);
        Assert.Contains(
            PaymentWorkflowSchemaGuard.OrderItemTemperatureMigrationId,
            defined);
    }

    [Fact]
    public void OrderItemTemperature_migration_type_has_Migration_attribute()
    {
        var migrationType = typeof(AppDbContext).Assembly
            .GetTypes()
            .Single(t => t.Name == "AddOrderItemTemperature" && typeof(Migration).IsAssignableFrom(t));

        var attr = migrationType.GetCustomAttribute<MigrationAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(OrderItemTemperatureMigrationId, attr!.Id);

        var ctxAttr = migrationType.GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(ctxAttr);
        Assert.Equal(typeof(AppDbContext), ctxAttr!.ContextType);
    }

    [Fact]
    public void RequiredOrderItemColumns_includes_Temperature()
    {
        Assert.Contains("Temperature", PaymentWorkflowSchemaGuard.RequiredOrderItemColumns, StringComparer.Ordinal);
    }

    [Fact]
    public void IsMissingPaymentColumnException_detects_Temperature_42703()
    {
        var ex = new Npgsql.PostgresException(
            "column oi.Temperature does not exist",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "42703");
        Assert.True(PaymentWorkflowSchemaGuard.IsMissingPaymentColumnException(ex));
    }

    private static AppDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=annap_schema_contract;Username=x;Password=x",
                npgsql => npgsql.UseVector())
            .Options;
        return new AppDbContext(options);
    }
}
