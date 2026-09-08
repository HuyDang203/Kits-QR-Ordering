using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGuidedSommelierNativeEnglishCopy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DescriptionEn",
            table: "experience_guided_questions",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PromptEn",
            table: "experience_guided_questions",
            type: "character varying(600)",
            maxLength: 600,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DescriptionEn",
            table: "experience_guided_options",
            type: "character varying(1200)",
            maxLength: 1200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LabelEn",
            table: "experience_guided_options",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SublineEn",
            table: "experience_guided_options",
            type: "character varying(400)",
            maxLength: 400,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DescriptionEn",
            table: "experience_guided_questions");

        migrationBuilder.DropColumn(
            name: "PromptEn",
            table: "experience_guided_questions");

        migrationBuilder.DropColumn(
            name: "DescriptionEn",
            table: "experience_guided_options");

        migrationBuilder.DropColumn(
            name: "LabelEn",
            table: "experience_guided_options");

        migrationBuilder.DropColumn(
            name: "SublineEn",
            table: "experience_guided_options");
    }
}
