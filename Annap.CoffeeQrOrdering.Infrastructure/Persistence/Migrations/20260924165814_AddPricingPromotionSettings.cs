using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingPromotionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_promotion_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_promotion_settings", x => x.Id);
                });

            // Seed singleton row in OFF state. Deploy must never auto-apply a
            // discount — the admin must explicitly toggle Enabled = true. The
            // Percent default of 10 matches the documented "Giảm giá 10%" UX.
            migrationBuilder.InsertData(
                table: "pricing_promotion_settings",
                columns: new[] { "Id", "Enabled", "Percent", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { new Guid("cccccccc-cccc-cccc-cccc-ccccccccccc1"), false, 10, new DateTimeOffset(2026, 9, 24, 16, 58, 14, TimeSpan.Zero), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_promotion_settings");
        }
    }
}
