using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddStaffAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PaymentConfirmedByAccountId",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "staff_accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastLoginAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_staff_accounts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_orders_PaymentConfirmedByAccountId",
            table: "orders",
            column: "PaymentConfirmedByAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_staff_accounts_IsActive",
            table: "staff_accounts",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_staff_accounts_Username",
            table: "staff_accounts",
            column: "Username",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "staff_accounts");

        migrationBuilder.DropIndex(
            name: "IX_orders_PaymentConfirmedByAccountId",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "PaymentConfirmedByAccountId",
            table: "orders");
    }
}
