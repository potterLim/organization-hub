using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class RecoverTodoDeadlineChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GoogleETag",
            table: "TodoTaskMetadata",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "TodoDeadlineChanges",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TaskListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                TaskId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ExpectedETag = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                DueAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoDeadlineChanges", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TodoDeadlineChanges_CreatedAt",
            table: "TodoDeadlineChanges",
            column: "CreatedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TodoDeadlineChanges");

        migrationBuilder.DropColumn(
            name: "GoogleETag",
            table: "TodoTaskMetadata");
    }
}
