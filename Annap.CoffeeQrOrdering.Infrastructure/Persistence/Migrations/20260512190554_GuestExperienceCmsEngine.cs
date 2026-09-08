using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestExperienceCmsEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscoveryStory",
                table: "menu_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscoveryEligible",
                table: "menu_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "experience_signature_slots",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "experience_guided_questions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "experience_guided_options",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlavorTagsJson",
                table: "experience_guided_options",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoodKey",
                table: "experience_guided_options",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefinementKey",
                table: "experience_guided_options",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightMultiplier",
                table: "experience_guided_options",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "AdventureTone",
                table: "experience_discovery_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRerolls",
                table: "experience_discovery_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSeasonalCups",
                table: "experience_discovery_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreferSignaturesFirst",
                table: "experience_discovery_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscoveryStory",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "IsDiscoveryEligible",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "experience_signature_slots");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "experience_guided_questions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "experience_guided_options");

            migrationBuilder.DropColumn(
                name: "FlavorTagsJson",
                table: "experience_guided_options");

            migrationBuilder.DropColumn(
                name: "MoodKey",
                table: "experience_guided_options");

            migrationBuilder.DropColumn(
                name: "RefinementKey",
                table: "experience_guided_options");

            migrationBuilder.DropColumn(
                name: "WeightMultiplier",
                table: "experience_guided_options");

            migrationBuilder.DropColumn(
                name: "AdventureTone",
                table: "experience_discovery_settings");

            migrationBuilder.DropColumn(
                name: "AllowRerolls",
                table: "experience_discovery_settings");

            migrationBuilder.DropColumn(
                name: "AllowSeasonalCups",
                table: "experience_discovery_settings");

            migrationBuilder.DropColumn(
                name: "PreferSignaturesFirst",
                table: "experience_discovery_settings");
        }
    }
}
