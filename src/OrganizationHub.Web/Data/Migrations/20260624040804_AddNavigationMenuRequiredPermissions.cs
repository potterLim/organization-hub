using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class AddNavigationMenuRequiredPermissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequiredPermissionKeys",
            table: "NavigationMenuItems",
            type: "TEXT",
            maxLength: 512,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RequiredPermissionKeys",
            table: "NavigationMenuItems");
    }
}
