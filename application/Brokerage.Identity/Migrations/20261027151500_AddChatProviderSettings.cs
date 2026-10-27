using Brokerage.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brokerage.Identity.Migrations;

[DbContext(typeof(IdentityDb))]
[Migration("20261027151500_AddChatProviderSettings")]
public sealed class AddChatProviderSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "chat_provider_settings",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EncryptedApiKey = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_chat_provider_settings", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "chat_provider_settings", schema: "identity");
    }
}
