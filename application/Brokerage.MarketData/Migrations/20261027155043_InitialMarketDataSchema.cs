using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.MarketData.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarketDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "market_data");

            migrationBuilder.CreateTable(
                name: "exchanges",
                schema: "market_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                schema: "market_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExchangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InstrumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_instruments_exchanges_ExchangeId",
                        column: x => x.ExchangeId,
                        principalSchema: "market_data",
                        principalTable: "exchanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "corporate_actions",
                schema: "market_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CashAmount = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Ratio = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_actions_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "market_data",
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_bars",
                schema: "market_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Interval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    High = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Volume = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_bars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_bars_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "market_data",
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                schema: "market_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Bid = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Ask = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quotes_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalSchema: "market_data",
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_InstrumentId_Reference",
                schema: "market_data",
                table: "corporate_actions",
                columns: new[] { "InstrumentId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_Code",
                schema: "market_data",
                table: "exchanges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instruments_ExchangeId_Symbol",
                schema: "market_data",
                table: "instruments",
                columns: new[] { "ExchangeId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_bars_InstrumentId_Interval_StartsAt",
                schema: "market_data",
                table: "price_bars",
                columns: new[] { "InstrumentId", "Interval", "StartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quotes_InstrumentId_ObservedAt",
                schema: "market_data",
                table: "quotes",
                columns: new[] { "InstrumentId", "ObservedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_actions",
                schema: "market_data");

            migrationBuilder.DropTable(
                name: "price_bars",
                schema: "market_data");

            migrationBuilder.DropTable(
                name: "quotes",
                schema: "market_data");

            migrationBuilder.DropTable(
                name: "instruments",
                schema: "market_data");

            migrationBuilder.DropTable(
                name: "exchanges",
                schema: "market_data");
        }
    }
}
