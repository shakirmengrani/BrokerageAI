using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.CorporateActions.Migrations
{
    /// <inheritdoc />
    public partial class InitialCorporateActionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "corporate_actions");

            migrationBuilder.CreateTable(
                name: "corporate_actions",
                schema: "corporate_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PayableDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CashAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    Ratio = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "corporate_action_elections",
                schema: "corporate_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorporateActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionOption = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corporate_action_elections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_corporate_action_elections_corporate_actions_CorporateActio~",
                        column: x => x.CorporateActionId,
                        principalSchema: "corporate_actions",
                        principalTable: "corporate_actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_action_elections_CorporateActionId_AccountId",
                schema: "corporate_actions",
                table: "corporate_action_elections",
                columns: new[] { "CorporateActionId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_ExDate_IsProcessed",
                schema: "corporate_actions",
                table: "corporate_actions",
                columns: new[] { "ExDate", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_corporate_actions_InstrumentId_ExternalReference",
                schema: "corporate_actions",
                table: "corporate_actions",
                columns: new[] { "InstrumentId", "ExternalReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "corporate_action_elections",
                schema: "corporate_actions");

            migrationBuilder.DropTable(
                name: "corporate_actions",
                schema: "corporate_actions");
        }
    }
}
