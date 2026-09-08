using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260510233000_HospitalityReliabilityOps")]
public partial class HospitalityReliabilityOps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ingredients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CurrentStock = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                LowStockThreshold = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ingredients", x => x.Id));

        migrationBuilder.CreateTable(
            name: "menu_item_ingredients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                QuantityRequired = table.Column<decimal>(type: "numeric(14,4)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_menu_item_ingredients", x => x.Id);
                table.ForeignKey(
                    name: "FK_menu_item_ingredients_menu_items_MenuItemId",
                    column: x => x.MenuItemId,
                    principalTable: "menu_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_menu_item_ingredients_ingredients_IngredientId",
                    column: x => x.IngredientId,
                    principalTable: "ingredients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_menu_item_ingredients_MenuItemId_IngredientId",
            table: "menu_item_ingredients",
            columns: new[] { "MenuItemId", "IngredientId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_menu_item_ingredients_IngredientId",
            table: "menu_item_ingredients",
            column: "IngredientId");

        migrationBuilder.CreateTable(
            name: "sommelier_suggestion_feedback",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                MoodKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                RefinementKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_sommelier_suggestion_feedback", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_sommelier_suggestion_feedback_SessionId",
            table: "sommelier_suggestion_feedback",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_sommelier_suggestion_feedback_MenuItemId",
            table: "sommelier_suggestion_feedback",
            column: "MenuItemId");

        migrationBuilder.AddColumn<string>(
            name: "BrewingOwnerStaffName",
            table: "orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ServingOwnerStaffName",
            table: "orders",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "StatusChangedAtUtc",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "orders" SET "StatusChangedAtUtc" = COALESCE("UpdatedAtUtc", "CreatedAtUtc")
            WHERE "StatusChangedAtUtc" IS NULL
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BrewingOwnerStaffName", table: "orders");
        migrationBuilder.DropColumn(name: "ServingOwnerStaffName", table: "orders");
        migrationBuilder.DropColumn(name: "StatusChangedAtUtc", table: "orders");

        migrationBuilder.DropTable(name: "sommelier_suggestion_feedback");
        migrationBuilder.DropTable(name: "menu_item_ingredients");
        migrationBuilder.DropTable(name: "ingredients");
    }
}
