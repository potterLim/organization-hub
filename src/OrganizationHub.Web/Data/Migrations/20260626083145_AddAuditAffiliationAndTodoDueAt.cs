using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class AddAuditAffiliationAndTodoDueAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "UploaderTeamId",
            table: "UploadAuditLogs",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UploaderTeamName",
            table: "UploadAuditLogs",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "DueAt",
            table: "TodoTaskMetadata",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ApproverPositionId",
            table: "ApprovalSteps",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApproverPositionName",
            table: "ApprovalSteps",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "ApproverTeamId",
            table: "ApprovalSteps",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApproverTeamName",
            table: "ApprovalSteps",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "RequesterPositionId",
            table: "ApprovalRequests",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RequesterPositionName",
            table: "ApprovalRequests",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "RequesterTeamId",
            table: "ApprovalRequests",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RequesterTeamName",
            table: "ApprovalRequests",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "ActorPositionId",
            table: "ApprovalAuditLogs",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorPositionName",
            table: "ApprovalAuditLogs",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "ActorTeamId",
            table: "ApprovalAuditLogs",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorTeamName",
            table: "ApprovalAuditLogs",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UploaderTeamId",
            table: "UploadAuditLogs");

        migrationBuilder.DropColumn(
            name: "UploaderTeamName",
            table: "UploadAuditLogs");

        migrationBuilder.DropColumn(
            name: "DueAt",
            table: "TodoTaskMetadata");

        migrationBuilder.DropColumn(
            name: "ApproverPositionId",
            table: "ApprovalSteps");

        migrationBuilder.DropColumn(
            name: "ApproverPositionName",
            table: "ApprovalSteps");

        migrationBuilder.DropColumn(
            name: "ApproverTeamId",
            table: "ApprovalSteps");

        migrationBuilder.DropColumn(
            name: "ApproverTeamName",
            table: "ApprovalSteps");

        migrationBuilder.DropColumn(
            name: "RequesterPositionId",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "RequesterPositionName",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "RequesterTeamId",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "RequesterTeamName",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "ActorPositionId",
            table: "ApprovalAuditLogs");

        migrationBuilder.DropColumn(
            name: "ActorPositionName",
            table: "ApprovalAuditLogs");

        migrationBuilder.DropColumn(
            name: "ActorTeamId",
            table: "ApprovalAuditLogs");

        migrationBuilder.DropColumn(
            name: "ActorTeamName",
            table: "ApprovalAuditLogs");
    }
}
