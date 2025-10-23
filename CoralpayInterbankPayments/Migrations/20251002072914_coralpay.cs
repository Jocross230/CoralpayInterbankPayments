using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralpayInterbankPayments.Migrations
{
    public partial class coralpay : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FTSingleRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    paymentRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    destinationInstitutionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    creditAccount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    creditAccountName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sourceAccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sourceAccountName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    narration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    group = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sector = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    nameEnquiryRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    transactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    responseCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    responseMessage = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FTSingleRequests", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FTSingleRequests");
        }
    }
}
