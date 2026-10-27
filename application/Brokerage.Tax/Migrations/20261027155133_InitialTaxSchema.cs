using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Tax.Migrations
{
    /// <inheritdoc />
    public partial class InitialTaxSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tax");

            migrationBuilder.CreateTable(
                name: "tax_lots",
                schema: "tax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    CostBasis = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tax_statements",
                schema: "tax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    Jurisdiction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_statements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "withholdings",
                schema: "tax",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TaxDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholdings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_lots_AccountId_AcquisitionReference",
                schema: "tax",
                table: "tax_lots",
                columns: new[] { "AccountId", "AcquisitionReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_lots_AccountId_InstrumentId_AcquiredOn",
                schema: "tax",
                table: "tax_lots",
                columns: new[] { "AccountId", "InstrumentId", "AcquiredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_statements_AccountId_TaxYear_Jurisdiction",
                schema: "tax",
                table: "tax_statements",
                columns: new[] { "AccountId", "TaxYear", "Jurisdiction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withholdings_AccountId_PaymentReference",
                schema: "tax",
                table: "withholdings",
                columns: new[] { "AccountId", "PaymentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_withholdings_AccountId_TaxDate",
                schema: "tax",
                table: "withholdings",
                columns: new[] { "AccountId", "TaxDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_lots",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "tax_statements",
                schema: "tax");

            migrationBuilder.DropTable(
                name: "withholdings",
                schema: "tax");
        }
    }
}
