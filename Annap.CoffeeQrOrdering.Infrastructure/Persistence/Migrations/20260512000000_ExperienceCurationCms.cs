using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <summary>Experience CMS: menu discovery fields + signature / guided / discovery settings tables.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260512000000_ExperienceCurationCms")]
public partial class ExperienceCurationCms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DiscoveryWeight",
            table: "menu_items",
            type: "numeric(10,4)",
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.AddColumn<bool>(
            name: "IsHiddenDiscovery",
            table: "menu_items",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "StoryCopy",
            table: "menu_items",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MoodTags",
            table: "menu_items",
            type: "character varying(600)",
            maxLength: 600,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FlavorTags",
            table: "menu_items",
            type: "character varying(600)",
            maxLength: 600,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "menu_items" SET "DiscoveryWeight" = 0;

            UPDATE "menu_items" SET "DiscoveryWeight" = 1
            WHERE "IsArchived" = FALSE AND "IsAvailable" = TRUE
              AND ("IsSignature" = TRUE OR "IsFeatured" = TRUE OR "IsSeasonalHighlight" = TRUE);
            """);

        migrationBuilder.CreateTable(
            name: "experience_discovery_settings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SeasonalOnlyPool = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CourierMoodCopy = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                FatigueCopyEvenLeg = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                FatigueCopyOddLeg = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                RerollPacingJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                RevealCopyNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_experience_discovery_settings", x => x.Id));

        migrationBuilder.Sql(
            """
            INSERT INTO "experience_discovery_settings" ("Id","SeasonalOnlyPool","CourierMoodCopy","FatigueCopyEvenLeg","FatigueCopyOddLeg","RerollPacingJson","RevealCopyNotes","CreatedAtUtc")
            SELECT 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', FALSE,
                'The house sends a quiet courier — no hurry in the wings.',
                'Please respect the courier.',
                'The courier has carried enough for tonight.',
                '{}', NULL, TIMESTAMPTZ '2026-05-10T00:00:00Z'
            WHERE NOT EXISTS (SELECT 1 FROM "experience_discovery_settings" LIMIT 1);
            """);

        migrationBuilder.CreateTable(
            name: "experience_signature_slots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsSpotlight = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                SeasonalSpotlightEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                EditorialKicker = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                EditorialBody = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_experience_signature_slots", x => x.Id);
                table.ForeignKey(
                    name: "FK_experience_signature_slots_menu_items_MenuItemId",
                    column: x => x.MenuItemId,
                    principalTable: "menu_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_experience_signature_slots_MenuItemId",
            table: "experience_signature_slots",
            column: "MenuItemId");

        migrationBuilder.CreateIndex(
            name: "IX_experience_signature_slots_SortOrder",
            table: "experience_signature_slots",
            column: "SortOrder");

        migrationBuilder.CreateTable(
            name: "experience_guided_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExternalKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Prompt = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsOptional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_experience_guided_questions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_experience_guided_questions_ExternalKey",
            table: "experience_guided_questions",
            column: "ExternalKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_experience_guided_questions_SortOrder",
            table: "experience_guided_questions",
            column: "SortOrder");

        migrationBuilder.CreateTable(
            name: "experience_guided_options",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                ExternalKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Subline = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                SensoryProfileJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_experience_guided_options", x => x.Id);
                table.ForeignKey(
                    name: "FK_experience_guided_options_experience_guided_questions_QuestionId",
                    column: x => x.QuestionId,
                    principalTable: "experience_guided_questions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_experience_guided_options_QuestionId_ExternalKey",
            table: "experience_guided_options",
            columns: new[] { "QuestionId", "ExternalKey" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "experience_guided_affinities",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Weight = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_experience_guided_affinities", x => x.Id);
                table.ForeignKey(
                    name: "FK_experience_guided_affinities_experience_guided_options_OptionId",
                    column: x => x.OptionId,
                    principalTable: "experience_guided_options",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_experience_guided_affinities_menu_items_MenuItemId",
                    column: x => x.MenuItemId,
                    principalTable: "menu_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_experience_guided_affinities_OptionId_MenuItemId",
            table: "experience_guided_affinities",
            columns: new[] { "OptionId", "MenuItemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_experience_guided_affinities_MenuItemId",
            table: "experience_guided_affinities",
            column: "MenuItemId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "experience_guided_affinities");
        migrationBuilder.DropTable(name: "experience_guided_options");
        migrationBuilder.DropTable(name: "experience_guided_questions");
        migrationBuilder.DropTable(name: "experience_signature_slots");
        migrationBuilder.DropTable(name: "experience_discovery_settings");

        migrationBuilder.DropColumn(name: "FlavorTags", table: "menu_items");
        migrationBuilder.DropColumn(name: "MoodTags", table: "menu_items");
        migrationBuilder.DropColumn(name: "StoryCopy", table: "menu_items");
        migrationBuilder.DropColumn(name: "IsHiddenDiscovery", table: "menu_items");
        migrationBuilder.DropColumn(name: "DiscoveryWeight", table: "menu_items");
    }
}
