using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StripUserIntegration.Migrations
{
    /// <inheritdoc />
    public partial class CreateTransferPaymentsTable_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferPaymentRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DestinationAccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlatformFee = table.Column<long>(type: "bigint", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferPaymentRequest", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferPaymentRequest");
        }
    }
}
