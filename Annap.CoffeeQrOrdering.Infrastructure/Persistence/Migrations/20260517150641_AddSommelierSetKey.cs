using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSommelierSetKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_experience_guided_questions_ExternalKey",
                table: "experience_guided_questions");

            migrationBuilder.AddColumn<string>(
                name: "SetKey",
                table: "experience_guided_questions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill: treat all pre-existing rows as the legacy v1 catalog.
            migrationBuilder.Sql(
                """UPDATE "experience_guided_questions" SET "SetKey" = 'atelier_v1' WHERE "SetKey" = '';""");

            migrationBuilder.CreateIndex(
                name: "IX_experience_guided_questions_SetKey_ExternalKey",
                table: "experience_guided_questions",
                columns: new[] { "SetKey", "ExternalKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_experience_guided_questions_SetKey_ExternalKey",
                table: "experience_guided_questions");

            migrationBuilder.DropColumn(
                name: "SetKey",
                table: "experience_guided_questions");

            migrationBuilder.CreateIndex(
                name: "IX_experience_guided_questions_ExternalKey",
                table: "experience_guided_questions",
                column: "ExternalKey",
                unique: true);
        }
    }
}
