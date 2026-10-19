using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class DiagnoseApprovalPublicationFailures : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AttemptCount",
            table: "ApprovalPublications",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastAttemptAt",
            table: "ApprovalPublications",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastError",
            table: "ApprovalPublications",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AttemptCount",
            table: "ApprovalPublications");

        migrationBuilder.DropColumn(
            name: "LastAttemptAt",
            table: "ApprovalPublications");

        migrationBuilder.DropColumn(
            name: "LastError",
            table: "ApprovalPublications");
    }
}

