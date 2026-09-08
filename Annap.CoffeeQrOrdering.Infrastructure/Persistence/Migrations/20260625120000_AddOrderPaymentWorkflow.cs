using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderPaymentWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_orders_Status_Valid",
            table: "orders");

        migrationBuilder.AddColumn<string>(
            name: "BillNumber",
            table: "orders",
            type: "character varying(24)",
            maxLength: 24,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompletedAtUtc",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PaidAtUtc",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaymentConfirmedBy",
            table: "orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PaymentMethod",
            table: "orders",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_orders_Status_Valid",
            table: "orders",
            sql: "\"Status\" IN (0, 1, 2, 3, 4, 5, 6, 7)");

        migrationBuilder.Sql(
            """
            UPDATE orders
            SET "CompletedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc")
            WHERE "Status" = 4 AND "CompletedAtUtc" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_orders_Status_Valid",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "BillNumber",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "CompletedAtUtc",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "PaidAtUtc",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "PaymentConfirmedBy",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "PaymentMethod",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "CK_orders_Status_Valid",
            table: "orders",
            sql: "\"Status\" IN (0, 1, 2, 3, 4, 5, 6)");
    }
}
