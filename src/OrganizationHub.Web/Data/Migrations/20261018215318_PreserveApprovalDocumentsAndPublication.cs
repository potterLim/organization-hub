using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class PreserveApprovalDocumentsAndPublication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DocumentHash",
            table: "ApprovalRequests",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "Revision",
            table: "ApprovalRequests",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "ApprovalDocuments",
            columns: table => new
            {
                ApprovalRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                FileName = table.Column<string>(type: "TEXT", nullable: false),
                ContentType = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalDocuments", x => x.ApprovalRequestId);
                table.ForeignKey(
                    name: "FK_ApprovalDocuments_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalPublications",
            columns: table => new
            {
                ApprovalRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                TargetFileId = table.Column<string>(type: "TEXT", nullable: false),
                FileName = table.Column<string>(type: "TEXT", nullable: false),
                OperationKey = table.Column<string>(type: "TEXT", nullable: false),
                IsReplacement = table.Column<bool>(type: "INTEGER", nullable: false),
                IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                LeaseToken = table.Column<string>(type: "TEXT", nullable: false),
                LeaseExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalPublications", x => x.ApprovalRequestId);
                table.ForeignKey(
                    name: "FK_ApprovalPublications_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalPublications_OperationKey",
            table: "ApprovalPublications",
            column: "OperationKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalPublications_TargetFileId",
            table: "ApprovalPublications",
            column: "TargetFileId",
            unique: true,
            filter: "\"IsCompleted\" = 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApprovalDocuments");

        migrationBuilder.DropTable(
            name: "ApprovalPublications");

        migrationBuilder.DropColumn(
            name: "DocumentHash",
            table: "ApprovalRequests");

        migrationBuilder.DropColumn(
            name: "Revision",
            table: "ApprovalRequests");
    }
}
