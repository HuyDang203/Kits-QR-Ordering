using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MenuCatalogAndGroupExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CatalogKey",
                table: "menu_items",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconGlyph",
                table: "menu_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "menu_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experience_group_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArrivalKicker = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    GuestCountPrompt = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    GuestCountLead = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    MinGuests = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxGuests = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    GuestTabsIntro = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    GuestDoneHint = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    SummaryHeadline = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SummaryLead = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    HospitalityClosing = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_group_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_group_settings");

            migrationBuilder.DropColumn(
                name: "CatalogKey",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "IconGlyph",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "menu_items");
        }
    }
}
