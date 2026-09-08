using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_OrderItemSnapshotAndSafety : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MenuItemName",
                table: "order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE order_items oi
SET ""MenuItemName"" = mi.""Name""
FROM menu_items mi
WHERE oi.""MenuItemId"" = mi.""Id""
  AND oi.""MenuItemName"" IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_menu_items_MenuItemId",
                table: "order_items");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_menu_items_MenuItemId",
                table: "order_items",
                column: "MenuItemId",
                principalTable: "menu_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
ALTER TABLE orders
ADD CONSTRAINT ""CK_orders_Status_Valid""
CHECK (""Status"" IN (0, 1, 2, 3, 4, 5, 6));");

            migrationBuilder.Sql(@"
ALTER TABLE order_items
ADD CONSTRAINT ""CK_order_items_Quantity_Positive""
CHECK (""Quantity"" > 0);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE order_items
DROP CONSTRAINT IF EXISTS ""CK_order_items_Quantity_Positive"";");

            migrationBuilder.Sql(@"
ALTER TABLE orders
DROP CONSTRAINT IF EXISTS ""CK_orders_Status_Valid"";");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_menu_items_MenuItemId",
                table: "order_items");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_menu_items_MenuItemId",
                table: "order_items",
                column: "MenuItemId",
                principalTable: "menu_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "MenuItemName",
                table: "order_items");
        }
    }
}

