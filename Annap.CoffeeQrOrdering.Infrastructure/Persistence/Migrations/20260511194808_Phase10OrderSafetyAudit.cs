using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10OrderSafetyAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmitIdempotencyKey",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operational_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActionKind = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_SubmitIdempotencyKey",
                table: "orders",
                column: "SubmitIdempotencyKey",
                unique: true,
                filter: "\"SubmitIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_operational_audit_entries_OccurredAtUtc",
                table: "operational_audit_entries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_operational_audit_entries_OrderId",
                table: "operational_audit_entries",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_audit_entries");

            migrationBuilder.DropIndex(
                name: "IX_orders_SubmitIdempotencyKey",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SubmitIdempotencyKey",
                table: "orders");
        }
    }
}
