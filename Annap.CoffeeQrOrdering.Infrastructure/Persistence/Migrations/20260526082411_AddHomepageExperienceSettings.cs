using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageExperienceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "homepage_experience_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsGroupEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSoloEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSommelierEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homepage_experience_settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "homepage_experience_settings",
                columns: new[] { "Id", "IsGroupEnabled", "IsSoloEnabled", "IsSommelierEnabled", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb8"), true, true, true, new DateTimeOffset(2026, 5, 26, 0, 0, 0, TimeSpan.Zero), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "homepage_experience_settings");
        }
    }
}
