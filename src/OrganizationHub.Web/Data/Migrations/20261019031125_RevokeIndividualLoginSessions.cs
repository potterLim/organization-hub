using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class RevokeIndividualLoginSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RevokedLoginSessions",
            columns: table => new
            {
                SessionId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                UserAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RevokedLoginSessions", x => x.SessionId);
                table.ForeignKey(
                    name: "FK_RevokedLoginSessions_UserAccounts_UserAccountId",
                    column: x => x.UserAccountId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RevokedLoginSessions_UserAccountId",
            table: "RevokedLoginSessions",
            column: "UserAccountId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RevokedLoginSessions");
    }
}

