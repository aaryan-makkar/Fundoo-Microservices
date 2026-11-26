using Fundoo.User.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fundoo.User.Infrastructure.Persistence.Migrations;

[DbContext(typeof(UserDbContext))]
[Migration("20261126120000_AddPreviousRefreshTokenHash")]
public partial class AddPreviousRefreshTokenHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreviousRefreshTokenHash",
            table: "UserSessions",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_PreviousRefreshTokenHash",
            table: "UserSessions",
            column: "PreviousRefreshTokenHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserSessions_PreviousRefreshTokenHash",
            table: "UserSessions");

        migrationBuilder.DropColumn(
            name: "PreviousRefreshTokenHash",
            table: "UserSessions");
    }
}
