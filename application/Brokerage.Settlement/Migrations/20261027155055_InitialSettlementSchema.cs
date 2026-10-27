using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Settlement.Migrations
{
    /// <inheritdoc />
    public partial class InitialSettlementSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settlement");

            migrationBuilder.CreateTable(
                name: "settlement_batches",
                schema: "settlement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_instructions",
                schema: "settlement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    CashAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_instructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settlement_instructions_settlement_batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "settlement",
                        principalTable: "settlement_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlement_failures",
                schema: "settlement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_failures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settlement_failures_settlement_instructions_InstructionId",
                        column: x => x.InstructionId,
                        principalSchema: "settlement",
                        principalTable: "settlement_instructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_batches_BatchReference",
                schema: "settlement",
                table: "settlement_batches",
                column: "BatchReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlement_failures_InstructionId_DetectedAt",
                schema: "settlement",
                table: "settlement_failures",
                columns: new[] { "InstructionId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_instructions_BatchId",
                schema: "settlement",
                table: "settlement_instructions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_instructions_ExternalReference",
                schema: "settlement",
                table: "settlement_instructions",
                column: "ExternalReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlement_instructions_Status_SettlementDate",
                schema: "settlement",
                table: "settlement_instructions",
                columns: new[] { "Status", "SettlementDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_failures",
                schema: "settlement");

            migrationBuilder.DropTable(
                name: "settlement_instructions",
                schema: "settlement");

            migrationBuilder.DropTable(
                name: "settlement_batches",
                schema: "settlement");
        }
    }
}
