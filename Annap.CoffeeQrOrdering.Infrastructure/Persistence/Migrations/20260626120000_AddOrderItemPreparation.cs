using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderItemPreparation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PreparedAtUtc",
            table: "order_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PreparedBy",
            table: "order_items",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PreparedQuantity",
            table: "order_items",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_order_items_PreparedQuantity_Range",
            table: "order_items",
            sql: "\"PreparedQuantity\" >= 0 AND \"PreparedQuantity\" <= \"Quantity\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_order_items_PreparedQuantity_Range",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "PreparedAtUtc",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "PreparedBy",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "PreparedQuantity",
            table: "order_items");
    }
}
