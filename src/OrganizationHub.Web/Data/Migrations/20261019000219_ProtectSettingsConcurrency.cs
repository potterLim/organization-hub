using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class ProtectSettingsConcurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DisplayOptionsRevision",
            table: "GlobalSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "FileResourcesRevision",
            table: "GlobalSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "Revision",
            table: "GlobalSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisplayOptionsRevision",
            table: "GlobalSettings");

        migrationBuilder.DropColumn(
            name: "FileResourcesRevision",
            table: "GlobalSettings");

        migrationBuilder.DropColumn(
            name: "Revision",
            table: "GlobalSettings");
    }
}
