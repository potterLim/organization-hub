using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class AddAuditLogPositionSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "UploaderPositionId",
            table: "UploadAuditLogs",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UploaderPositionName",
            table: "UploadAuditLogs",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "DeletedByPositionId",
            table: "CalendarEventDeletionAuditLogs",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeletedByPositionName",
            table: "CalendarEventDeletionAuditLogs",
            type: "TEXT",
            maxLength: 120,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UploaderPositionId",
            table: "UploadAuditLogs");

        migrationBuilder.DropColumn(
            name: "UploaderPositionName",
            table: "UploadAuditLogs");

        migrationBuilder.DropColumn(
            name: "DeletedByPositionId",
            table: "CalendarEventDeletionAuditLogs");

        migrationBuilder.DropColumn(
            name: "DeletedByPositionName",
            table: "CalendarEventDeletionAuditLogs");
    }
}
