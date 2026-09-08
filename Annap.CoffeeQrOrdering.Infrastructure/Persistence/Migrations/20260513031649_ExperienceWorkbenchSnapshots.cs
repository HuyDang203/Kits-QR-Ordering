using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExperienceWorkbenchSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experience_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)0),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    HouseNote = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "experience_publish_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_publish_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experience_publish_records_experience_snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "experience_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experience_publish_records_CreatedAtUtc",
                table: "experience_publish_records",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_experience_publish_records_SnapshotId",
                table: "experience_publish_records",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_snapshots_Kind_CreatedAtUtc",
                table: "experience_snapshots",
                columns: new[] { "Kind", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_publish_records");

            migrationBuilder.DropTable(
                name: "experience_snapshots");
        }
    }
}
