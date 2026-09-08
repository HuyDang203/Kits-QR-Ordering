using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VenueTablesAndGuestOrderTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestSessionToken",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueTableId",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "venue_tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PublicSlug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_tables", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_GuestSessionToken",
                table: "orders",
                column: "GuestSessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_VenueTableId",
                table: "orders",
                column: "VenueTableId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_tables_PublicSlug",
                table: "venue_tables",
                column: "PublicSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_tables_VenueCode_DisplayCode",
                table: "venue_tables",
                columns: new[] { "VenueCode", "DisplayCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_venue_tables_VenueTableId",
                table: "orders",
                column: "VenueTableId",
                principalTable: "venue_tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_venue_tables_VenueTableId",
                table: "orders");

            migrationBuilder.DropTable(
                name: "venue_tables");

            migrationBuilder.DropIndex(
                name: "IX_orders_GuestSessionToken",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_VenueTableId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "GuestSessionToken",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "VenueTableId",
                table: "orders");
        }
    }
}
