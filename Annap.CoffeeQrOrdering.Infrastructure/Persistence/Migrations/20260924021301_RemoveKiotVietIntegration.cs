using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKiotVietIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop foreign keys / dependencies safely first ────────────
            // DropTable would cascade the FK away, but listing DropForeignKey
            // explicitly makes the dependency order unambiguous and keeps the
            // migration independent of provider-specific cascade behaviour.
            migrationBuilder.DropForeignKey(
                name: "FK_kiotviet_outbox_messages_orders_OrderId",
                table: "kiotviet_outbox_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_kiotviet_product_mappings_menu_items_MenuItemId",
                table: "kiotviet_product_mappings");

            // ── 2. Drop the indexes owned by KiotViet tables ─────────────────
            migrationBuilder.DropIndex(
                name: "ix_kv_outbox_order_id",
                table: "kiotviet_outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_kv_outbox_status_retry",
                table: "kiotviet_outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_kv_product_code",
                table: "kiotviet_product_mappings");

            migrationBuilder.DropIndex(
                name: "ix_kv_sync_logs_kind_success_time",
                table: "kiotviet_sync_logs");

            migrationBuilder.DropIndex(
                name: "ix_kv_sync_logs_time",
                table: "kiotviet_sync_logs");

            // ── 3. Drop the KiotViet tables (exclusively KiotViet-owned) ────
            migrationBuilder.DropTable(
                name: "kiotviet_outbox_messages");

            migrationBuilder.DropTable(
                name: "kiotviet_product_mappings");

            migrationBuilder.DropTable(
                name: "kiotviet_sync_logs");

            // ── 4. Drop KiotViet-only columns from venue_tables ─────────────
            // venue_tables is a production table; only the two KiotViet columns
            // (added by AddKiotVietIntegrationPhase1A) are removed.
            migrationBuilder.DropColumn(
                name: "KiotVietBranchId",
                table: "venue_tables");

            migrationBuilder.DropColumn(
                name: "KiotVietTableId",
                table: "venue_tables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KiotVietBranchId",
                table: "venue_tables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KiotVietTableId",
                table: "venue_tables",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "kiotviet_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    KiotVietOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiotviet_outbox_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kiotviet_outbox_messages_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kiotviet_product_mappings",
                columns: table => new
                {
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    KiotVietProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KiotVietProductName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiotviet_product_mappings", x => x.MenuItemId);
                    table.ForeignKey(
                        name: "FK_kiotviet_product_mappings_menu_items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kiotviet_sync_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    KiotVietReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SyncKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiotviet_sync_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kv_outbox_order_id",
                table: "kiotviet_outbox_messages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "ix_kv_outbox_status_retry",
                table: "kiotviet_outbox_messages",
                columns: new[] { "Status", "NextRetryAtUtc" },
                filter: "\"Status\" IN (0, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_kv_product_code",
                table: "kiotviet_product_mappings",
                column: "KiotVietProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kv_sync_logs_kind_success_time",
                table: "kiotviet_sync_logs",
                columns: new[] { "SyncKind", "IsSuccess", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_kv_sync_logs_time",
                table: "kiotviet_sync_logs",
                column: "OccurredAtUtc",
                descending: new bool[0]);
        }
    }
}
