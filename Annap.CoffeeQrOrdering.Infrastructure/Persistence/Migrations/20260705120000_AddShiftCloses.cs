using Microsoft.EntityFrameworkCore.Migrations;



#nullable disable



namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Migrations;



/// <inheritdoc />

public partial class AddShiftCloses : Migration

{

    protected override void Up(MigrationBuilder migrationBuilder)

    {

        migrationBuilder.CreateTable(

            name: "shift_closes",

            columns: table => new

            {

                Id = table.Column<Guid>(type: "uuid", nullable: false),

                OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),

                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),

                ClosedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),

                ClosedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),

                TotalOrders = table.Column<int>(type: "integer", nullable: false),

                TotalGrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),

                CashOrCardAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),

                BankTransferAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),

                UnknownPaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),

                CashOrCardOrders = table.Column<int>(type: "integer", nullable: false),

                BankTransferOrders = table.Column<int>(type: "integer", nullable: false),

                UnknownPaymentOrders = table.Column<int>(type: "integer", nullable: false),

                SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),

                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),

                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)

            },

            constraints: table =>

            {

                table.PrimaryKey("PK_shift_closes", x => x.Id);

            });



        migrationBuilder.CreateIndex(

            name: "IX_shift_closes_ClosedAtUtc",

            table: "shift_closes",

            column: "ClosedAtUtc");



        migrationBuilder.CreateIndex(

            name: "IX_shift_closes_ClosedByAccountId",

            table: "shift_closes",

            column: "ClosedByAccountId");

    }



    protected override void Down(MigrationBuilder migrationBuilder)

    {

        migrationBuilder.DropTable(name: "shift_closes");

    }

}


