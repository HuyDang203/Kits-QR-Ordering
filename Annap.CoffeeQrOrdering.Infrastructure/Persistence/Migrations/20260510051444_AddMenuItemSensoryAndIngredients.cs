using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemSensoryAndIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcidityLevel",
                table: "menu_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CaffeineLevel",
                table: "menu_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngredientBreakdown",
                table: "menu_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SweetnessLevel",
                table: "menu_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcidityLevel",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "CaffeineLevel",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "IngredientBreakdown",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "SweetnessLevel",
                table: "menu_items");
        }
    }
}
