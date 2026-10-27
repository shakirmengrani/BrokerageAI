using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Reconciliation.Migrations
{
    /// <inheritdoc />
    public partial class InitialReconciliationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reconciliation");

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                schema: "reconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_items",
                schema: "reconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InternalAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    ExternalAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reconciliation_items_reconciliation_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "reconciliation",
                        principalTable: "reconciliation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_exceptions",
                schema: "reconciliation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_exceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reconciliation_exceptions_reconciliation_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "reconciliation",
                        principalTable: "reconciliation_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_exceptions_ItemId",
                schema: "reconciliation",
                table: "reconciliation_exceptions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_exceptions_Status_CreatedAt",
                schema: "reconciliation",
                table: "reconciliation_exceptions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_items_RunId_Status",
                schema: "reconciliation",
                table: "reconciliation_items",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_Source_RunReference",
                schema: "reconciliation",
                table: "reconciliation_runs",
                columns: new[] { "Source", "RunReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconciliation_exceptions",
                schema: "reconciliation");

            migrationBuilder.DropTable(
                name: "reconciliation_items",
                schema: "reconciliation");

            migrationBuilder.DropTable(
                name: "reconciliation_runs",
                schema: "reconciliation");
        }
    }
}
