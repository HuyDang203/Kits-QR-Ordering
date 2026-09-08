using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMenuItemSensoryProfileJson : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SensoryProfile",
            table: "menu_items",
            type: "jsonb",
            nullable: true);

        migrationBuilder.Sql("""UPDATE "menu_items" SET "SensoryProfile" = '{}'::jsonb WHERE "SensoryProfile" IS NULL;""");

        migrationBuilder.Sql("""UPDATE "menu_items" SET "EmbeddingModel" = NULL WHERE "Embedding" IS NOT NULL;""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SensoryProfile",
            table: "menu_items");
    }
}
