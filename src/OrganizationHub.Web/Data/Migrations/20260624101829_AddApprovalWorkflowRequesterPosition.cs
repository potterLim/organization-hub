using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class AddApprovalWorkflowRequesterPosition : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamId_Priority",
            table: "ApprovalWorkflows");

        migrationBuilder.AddColumn<int>(
            name: "AppliesToPositionId",
            table: "ApprovalWorkflows",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflows_AppliesToPositionId",
            table: "ApprovalWorkflows",
            column: "AppliesToPositionId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamId_AppliesToPositionId_Priority",
            table: "ApprovalWorkflows",
            columns: new[] { "AppliesToTeamId", "AppliesToPositionId", "Priority" });

        migrationBuilder.AddForeignKey(
            name: "FK_ApprovalWorkflows_Positions_AppliesToPositionId",
            table: "ApprovalWorkflows",
            column: "AppliesToPositionId",
            principalTable: "Positions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ApprovalWorkflows_Positions_AppliesToPositionId",
            table: "ApprovalWorkflows");

        migrationBuilder.DropIndex(
            name: "IX_ApprovalWorkflows_AppliesToPositionId",
            table: "ApprovalWorkflows");

        migrationBuilder.DropIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamId_AppliesToPositionId_Priority",
            table: "ApprovalWorkflows");

        migrationBuilder.DropColumn(
            name: "AppliesToPositionId",
            table: "ApprovalWorkflows");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamId_Priority",
            table: "ApprovalWorkflows",
            columns: new[] { "AppliesToTeamId", "Priority" });
    }
}
