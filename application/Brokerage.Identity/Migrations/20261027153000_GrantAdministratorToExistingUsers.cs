using Brokerage.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brokerage.Identity.Migrations;

[DbContext(typeof(IdentityDb))]
[Migration("20261027153000_GrantAdministratorToExistingUsers")]
public sealed class GrantAdministratorToExistingUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO identity.roles ("Id", "Name", "NormalizedName")
            VALUES ('d3f0ac48-32bf-4a2d-b6f8-0a87e9e84b29', 'Administrator', 'ADMINISTRATOR')
            ON CONFLICT ("NormalizedName") DO NOTHING;

            INSERT INTO identity.user_roles ("UserId", "RoleId")
            SELECT users."Id", roles."Id"
            FROM identity.users AS users
            CROSS JOIN identity.roles AS roles
            WHERE roles."NormalizedName" = 'ADMINISTRATOR'
            ON CONFLICT ("UserId", "RoleId") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "This migration grants Administrator access to every existing account. Revoke those assignments only after reviewing the affected users.");
    }
}
