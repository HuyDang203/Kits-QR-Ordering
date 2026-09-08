using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>
/// Detects whether order workflow EF migrations have been applied to PostgreSQL.
/// Required column lists are the production schema contract — keep them aligned with mapped entities.
/// </summary>
internal static class PaymentWorkflowSchemaGuard
{
    public const string MigrationRequiredErrorCode = "database_migration_required";

    public const string StartupFailureMessage =
        "Order workflow migrations are not applied. Required order or order-item columns are missing. Run database migrations before starting the application.";

    public const string ApiMessageEn = "Order workflow migration is not applied.";

    public const string ApiMessageVi =
        "Cơ sở dữ liệu chưa được cập nhật cho quy trình đơn hàng. Vui lòng chạy migration rồi thử lại.";

    public const string EfUpdateCommand =
        "dotnet ef database update --project Annap.CoffeeQrOrdering.Infrastructure --startup-project Annap.CoffeeQrOrdering.Web";

    public const string OrderItemTemperatureMigrationId = "20260805120000_AddOrderItemTemperature";

    public static readonly IReadOnlyList<string> RequiredOrderColumns =
    [
        "BillNumber",
        "PaidAtUtc",
        "PaymentConfirmedBy",
        "PaymentMethod",
        "CompletedAtUtc"
    ];

    /// <summary>
    /// Every scalar column mapped on <c>OrderItem</c>. Missing any of these must fail fast
    /// with <see cref="MigrationRequiredErrorCode"/> — never a hidden 500.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredOrderItemColumns =
    [
        "Id",
        "OrderId",
        "MenuItemId",
        "Quantity",
        "UnitPrice",
        "Notes",
        "CustomerNote",
        "Temperature",
        "MenuItemName",
        "PreparedQuantity",
        "PreparedAtUtc",
        "PreparedBy",
        "PreparedByAccountId"
    ];

    public static async Task<IReadOnlyList<string>> GetMissingColumnsAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            return RequiredOrderColumns.Concat(RequiredOrderItemColumns).ToList();

        var presentOrders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var presentItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var orderIn = string.Join(", ", RequiredOrderColumns.Select(QuoteIdent));
        var itemIn = string.Join(", ", RequiredOrderItemColumns.Select(QuoteIdent));

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND (
                (table_name = 'orders' AND column_name IN ({orderIn}))
                OR (table_name = 'order_items' AND column_name IN ({itemIn}))
              )
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var table = reader.GetString(0);
            var column = reader.GetString(1);
            if (table.Equals("orders", StringComparison.OrdinalIgnoreCase))
                presentOrders.Add(column);
            else if (table.Equals("order_items", StringComparison.OrdinalIgnoreCase))
                presentItems.Add(column);
        }

        var missing = RequiredOrderColumns.Where(c => !presentOrders.Contains(c)).ToList();
        missing.AddRange(RequiredOrderItemColumns.Where(c => !presentItems.Contains(c)));
        return missing;
    }

    public static bool IsMissingPaymentColumnException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is not PostgresException pg || pg.SqlState != "42703")
                continue;

            var message = pg.MessageText ?? pg.Message;
            if (RequiredOrderColumns.Concat(RequiredOrderItemColumns).Any(c =>
                    message.Contains(c, StringComparison.OrdinalIgnoreCase)
                    || message.Contains($"\"{c}\"", StringComparison.Ordinal)))
                return true;

            if (message.Contains("orders", StringComparison.OrdinalIgnoreCase)
                || message.Contains("order_items", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static IResult MigrationRequiredResult() =>
        Results.Json(
            new { error = MigrationRequiredErrorCode, message = ApiMessageEn },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static string QuoteIdent(string name) =>
        "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'";
}
