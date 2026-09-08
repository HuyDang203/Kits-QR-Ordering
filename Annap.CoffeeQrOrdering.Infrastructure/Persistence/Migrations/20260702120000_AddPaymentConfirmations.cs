using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPaymentConfirmations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "payment_confirmations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProviderTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                BankCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                RawPayloadJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                MatchedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                MatchStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_confirmations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_payment_confirmations_MatchedOrderId",
            table: "payment_confirmations",
            column: "MatchedOrderId");

        migrationBuilder.CreateIndex(
            name: "IX_payment_confirmations_Memo",
            table: "payment_confirmations",
            column: "Memo");

        migrationBuilder.CreateIndex(
            name: "IX_payment_confirmations_Provider_ProviderTransactionId",
            table: "payment_confirmations",
            columns: new[] { "Provider", "ProviderTransactionId" },
            unique: true,
            filter: "\"ProviderTransactionId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_payment_confirmations_ReceivedAtUtc",
            table: "payment_confirmations",
            column: "ReceivedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "payment_confirmations");
    }
}
