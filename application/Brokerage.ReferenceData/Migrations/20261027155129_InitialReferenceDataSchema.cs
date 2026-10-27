using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.ReferenceData.Migrations
{
    /// <inheritdoc />
    public partial class InitialReferenceDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reference_data");

            migrationBuilder.CreateTable(
                name: "business_calendars",
                schema: "reference_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "classifications",
                schema: "reference_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassificationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "reference_data",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MinorUnits = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.Code);
                    table.CheckConstraint("ck_currency_minor_units", "\"MinorUnits\" BETWEEN 0 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "business_calendar_days",
                schema: "reference_data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsBusinessDay = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_calendar_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_calendar_days_business_calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "reference_data",
                        principalTable: "business_calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                schema: "reference_data",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.Code);
                    table.ForeignKey(
                        name: "FK_countries_currencies_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalSchema: "reference_data",
                        principalTable: "currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_calendar_days_CalendarId_Date",
                schema: "reference_data",
                table: "business_calendar_days",
                columns: new[] { "CalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_calendars_Code",
                schema: "reference_data",
                table: "business_calendars",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_classifications_ClassificationType_Code",
                schema: "reference_data",
                table: "classifications",
                columns: new[] { "ClassificationType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_countries_CurrencyCode",
                schema: "reference_data",
                table: "countries",
                column: "CurrencyCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_calendar_days",
                schema: "reference_data");

            migrationBuilder.DropTable(
                name: "classifications",
                schema: "reference_data");

            migrationBuilder.DropTable(
                name: "countries",
                schema: "reference_data");

            migrationBuilder.DropTable(
                name: "business_calendars",
                schema: "reference_data");

            migrationBuilder.DropTable(
                name: "currencies",
                schema: "reference_data");
        }
    }
}
