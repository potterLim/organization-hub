using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;
/// <inheritdoc />
public partial class GuardAuditRecoveryAndUploadIntents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsUploadConfirmed", table: "PendingUploadAudits", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "LastAttemptAt",
            table: "PendingUploadAudits",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OperationKey",
            table: "PendingUploadAudits",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "LastRecoveryAttemptAt",
            table: "CalendarEventDeletionAuditLogs",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsUploadConfirmed", table: "PendingUploadAudits");
        migrationBuilder.DropColumn(
            name: "LastAttemptAt",
            table: "PendingUploadAudits");

        migrationBuilder.DropColumn(
            name: "OperationKey",
            table: "PendingUploadAudits");

        migrationBuilder.DropColumn(
            name: "LastRecoveryAttemptAt",
            table: "CalendarEventDeletionAuditLogs");
    }
}
