using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class AddNavigationMenuSections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NavigationMenuSections",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SectionKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NavigationMenuSections", x => x.Id);
            });

        migrationBuilder.InsertData(
            table: "NavigationMenuSections",
            columns: new[] { "Id", "SectionKey", "DisplayName", "SortOrder" },
            values: new object[,]
            {
                { 1, "Primary", "기본", 10 },
                { 2, "Operations", "운영", 20 },
                { 3, "Resources", "자료실", 30 },
                { 4, "Approvals", "결재", 40 },
                { 5, "Administration", "관리", 50 }
            });

        migrationBuilder.AddColumn<int>(
            name: "SectionId",
            table: "NavigationMenuItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.Sql(
            "UPDATE NavigationMenuItems SET SectionId = 2 WHERE MenuKey IN ('Calendar', 'Todos', 'WorkGuidelines', 'WorkResources');");
        migrationBuilder.Sql(
            "UPDATE NavigationMenuItems SET SectionId = 3 WHERE MenuKey IN ('Materials', 'ResponsibleResources', 'ManagementFiles', 'Common', 'Public', 'Upload');");
        migrationBuilder.Sql(
            "UPDATE NavigationMenuItems SET SectionId = 4 WHERE MenuKey IN ('ApprovalRequest', 'ApprovalInbox', 'ApprovalHistory', 'ApprovedFiles');");
        migrationBuilder.Sql(
            "UPDATE NavigationMenuItems SET SectionId = 5 WHERE MenuKey IN ('AdminUsers', 'AdminTeams', 'AdminPositions', 'AdminApprovalWorkflows', 'AdminSettings', 'AdminMenus', 'AdminGoogle', 'AdminCalendarAudit');");

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuItems_SectionId",
            table: "NavigationMenuItems",
            column: "SectionId");

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuSections_SectionKey",
            table: "NavigationMenuSections",
            column: "SectionKey",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_NavigationMenuItems_NavigationMenuSections_SectionId",
            table: "NavigationMenuItems",
            column: "SectionId",
            principalTable: "NavigationMenuSections",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_NavigationMenuItems_NavigationMenuSections_SectionId",
            table: "NavigationMenuItems");

        migrationBuilder.DropIndex(
            name: "IX_NavigationMenuItems_SectionId",
            table: "NavigationMenuItems");

        migrationBuilder.DropColumn(
            name: "SectionId",
            table: "NavigationMenuItems");

        migrationBuilder.DropTable(
            name: "NavigationMenuSections");
    }
}
