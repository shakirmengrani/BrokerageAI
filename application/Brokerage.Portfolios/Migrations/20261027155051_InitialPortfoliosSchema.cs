using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Portfolios.Migrations
{
    /// <inheritdoc />
    public partial class InitialPortfoliosSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "portfolios");

            migrationBuilder.CreateTable(
                name: "portfolios",
                schema: "portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    AverageCost = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_positions_portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalSchema: "portfolios",
                        principalTable: "portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valuations",
                schema: "portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    CashValue = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_valuations_portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalSchema: "portfolios",
                        principalTable: "portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_lots",
                schema: "portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenQuantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_position_lots_positions_PositionId",
                        column: x => x.PositionId,
                        principalSchema: "portfolios",
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portfolios_ClientId_Name",
                schema: "portfolios",
                table: "portfolios",
                columns: new[] { "ClientId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_position_lots_PositionId_SourceReference",
                schema: "portfolios",
                table: "position_lots",
                columns: new[] { "PositionId", "SourceReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_PortfolioId_InstrumentId",
                schema: "portfolios",
                table: "positions",
                columns: new[] { "PortfolioId", "InstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valuations_PortfolioId_ValuedAt",
                schema: "portfolios",
                table: "valuations",
                columns: new[] { "PortfolioId", "ValuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "position_lots",
                schema: "portfolios");

            migrationBuilder.DropTable(
                name: "valuations",
                schema: "portfolios");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "portfolios");

            migrationBuilder.DropTable(
                name: "portfolios",
                schema: "portfolios");
        }
    }
}
