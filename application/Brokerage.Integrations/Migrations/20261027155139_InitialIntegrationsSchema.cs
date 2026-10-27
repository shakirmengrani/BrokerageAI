using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Integrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegrationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integrations");

            migrationBuilder.CreateTable(
                name: "integration_endpoints",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BaseAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_endpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_credential_references",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_credential_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_credential_references_integration_endpoints_End~",
                        column: x => x.EndpointId,
                        principalSchema: "integrations",
                        principalTable: "integration_endpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_messages",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_messages_integration_endpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalSchema: "integrations",
                        principalTable: "integration_endpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_credential_references_EndpointId",
                schema: "integrations",
                table: "integration_credential_references",
                column: "EndpointId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_endpoints_Provider_Name",
                schema: "integrations",
                table: "integration_endpoints",
                columns: new[] { "Provider", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_messages_EndpointId_MessageReference",
                schema: "integrations",
                table: "integration_messages",
                columns: new[] { "EndpointId", "MessageReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_messages_Status_CreatedAt",
                schema: "integrations",
                table: "integration_messages",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_credential_references",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_messages",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_endpoints",
                schema: "integrations");
        }
    }
}
