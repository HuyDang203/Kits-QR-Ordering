using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOrderGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_order_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByStaffName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedByStaffAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_order_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_order_group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByStaffName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddedByStaffAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_order_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_order_group_members_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_order_group_members_staff_order_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "staff_order_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_order_group_members_GroupId_OrderId",
                table: "staff_order_group_members",
                columns: new[] { "GroupId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_order_group_members_OrderId",
                table: "staff_order_group_members",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_order_groups_CreatedAtUtc",
                table: "staff_order_groups",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_order_group_members");

            migrationBuilder.DropTable(
                name: "staff_order_groups");
        }
    }
}
