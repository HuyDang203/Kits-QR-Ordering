using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKiotVietIntegrationPhase1A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KiotVietOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    KiotVietProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KiotVietProductName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    SyncKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    KiotVietReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kiotviet_outbox_messages");

            migrationBuilder.DropTable(
                name: "kiotviet_product_mappings");

            migrationBuilder.DropTable(
                name: "kiotviet_sync_logs");

            migrationBuilder.DropColumn(
                name: "KiotVietBranchId",
                table: "venue_tables");

            migrationBuilder.DropColumn(
                name: "KiotVietTableId",
                table: "venue_tables");
        }
    }
}
