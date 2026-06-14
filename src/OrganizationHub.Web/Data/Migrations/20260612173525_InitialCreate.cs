using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizationHub.Web.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CalendarEventDeletionAuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CalendarId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CalendarName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                EventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventSummary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                EventLocation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                EventEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                DeletedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                DeletedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                DeletedByRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                DeletedByTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                DeletedByTeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEventDeletionAuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CalendarEventMetadata",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CalendarId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEventMetadata", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GlobalSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                OfficialRootFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                PublicFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CommonFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleResourcesFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                WorkResourcesFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ArchiveFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                OrganizationCalendarId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleCalendarId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                OrganizationTaskListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleTaskListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NoticeFileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                WorkGuidelineFileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ShowTodoAuthor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                ShowCalendarAuthor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GlobalSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GoogleStorageConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ConnectedEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                EncryptedRefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                Scopes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                ConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastValidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoogleStorageConnections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "NavigationMenuItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                MenuKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Href = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                IconKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                AccessLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsVisibleToAllTeams = table.Column<bool>(type: "INTEGER", nullable: false),
                IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NavigationMenuItems", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Teams",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TeamKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                MaterialsFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ApprovedFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CalendarId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                TaskListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TodoTaskMetadata",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TaskListId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                TaskId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoTaskMetadata", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UploadAuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                FolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FolderDisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                FolderKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                UploaderUserId = table.Column<int>(type: "INTEGER", nullable: false),
                UploaderDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                UploaderRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                TeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadAuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalWorkflows",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                AppliesToTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflows_Teams_AppliesToTeamId",
                    column: x => x.AppliesToTeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NavigationMenuTeamPermissions",
            columns: table => new
            {
                NavigationMenuItemId = table.Column<int>(type: "INTEGER", nullable: false),
                TeamId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NavigationMenuTeamPermissions", x => new { x.NavigationMenuItemId, x.TeamId });
                table.ForeignKey(
                    name: "FK_NavigationMenuTeamPermissions_NavigationMenuItems_NavigationMenuItemId",
                    column: x => x.NavigationMenuItemId,
                    principalTable: "NavigationMenuItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_NavigationMenuTeamPermissions_Teams_TeamId",
                    column: x => x.TeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Positions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSystemAdministrator = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Positions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Positions_Teams_TeamId",
                    column: x => x.TeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PositionPermissionGrants",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PositionId = table.Column<int>(type: "INTEGER", nullable: false),
                PermissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TargetTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                IsAllowed = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PositionPermissionGrants", x => x.Id);
                table.ForeignKey(
                    name: "FK_PositionPermissionGrants_Positions_PositionId",
                    column: x => x.PositionId,
                    principalTable: "Positions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PositionPermissionGrants_Teams_TargetTeamId",
                    column: x => x.TargetTeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserAccounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                PositionId = table.Column<int>(type: "INTEGER", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserAccounts_Positions_PositionId",
                    column: x => x.PositionId,
                    principalTable: "Positions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccounts_Teams_TeamId",
                    column: x => x.TeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Title = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                FileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                MimeType = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                SourceFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                SourceFolderKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamId = table.Column<int>(type: "INTEGER", nullable: true),
                TeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                RequesterUserId = table.Column<int>(type: "INTEGER", nullable: false),
                RequesterDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StepCount = table.Column<int>(type: "INTEGER", nullable: false),
                CurrentStepNumber = table.Column<int>(type: "INTEGER", nullable: false),
                ApprovedFileId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ApprovedFolderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ApprovedFolderDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                RejectedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_Teams_TeamId",
                    column: x => x.TeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_UserAccounts_RequesterUserId",
                    column: x => x.RequesterUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalWorkflowSteps",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalWorkflowId = table.Column<int>(type: "INTEGER", nullable: false),
                StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                StepName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ApproverSelectionType = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                ApproverUserId = table.Column<int>(type: "INTEGER", nullable: true),
                ApproverPositionId = table.Column<int>(type: "INTEGER", nullable: true),
                ApproverTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                RequiredPermissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalWorkflowSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_ApprovalWorkflows_ApprovalWorkflowId",
                    column: x => x.ApprovalWorkflowId,
                    principalTable: "ApprovalWorkflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_Positions_ApproverPositionId",
                    column: x => x.ApproverPositionId,
                    principalTable: "Positions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_Teams_ApproverTeamId",
                    column: x => x.ApproverTeamId,
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_UserAccounts_ApproverUserId",
                    column: x => x.ApproverUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalAuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                ActorDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StepNumber = table.Column<int>(type: "INTEGER", nullable: true),
                Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                PreviousApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                NewApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalAuditLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalAuditLogs_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalSteps",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                StepNumber = table.Column<int>(type: "INTEGER", nullable: false),
                ApproverUserId = table.Column<int>(type: "INTEGER", nullable: false),
                ApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ActionedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalSteps_ApprovalRequests_ApprovalRequestId",
                    column: x => x.ApprovalRequestId,
                    principalTable: "ApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApprovalSteps_UserAccounts_ApproverUserId",
                    column: x => x.ApproverUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_Action",
            table: "ApprovalAuditLogs",
            column: "Action");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_ActorUserId",
            table: "ApprovalAuditLogs",
            column: "ActorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_ApprovalRequestId",
            table: "ApprovalAuditLogs",
            column: "ApprovalRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_CreatedAt",
            table: "ApprovalAuditLogs",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_ApprovedFileId",
            table: "ApprovalRequests",
            column: "ApprovedFileId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_FileId",
            table: "ApprovalRequests",
            column: "FileId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_RequesterUserId",
            table: "ApprovalRequests",
            column: "RequesterUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_Status",
            table: "ApprovalRequests",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_TeamId",
            table: "ApprovalRequests",
            column: "TeamId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_ApprovalRequestId_StepNumber",
            table: "ApprovalSteps",
            columns: new[] { "ApprovalRequestId", "StepNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_ApproverUserId",
            table: "ApprovalSteps",
            column: "ApproverUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_Status",
            table: "ApprovalSteps",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamId_Priority",
            table: "ApprovalWorkflows",
            columns: new[] { "AppliesToTeamId", "Priority" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApprovalWorkflowId_StepOrder",
            table: "ApprovalWorkflowSteps",
            columns: new[] { "ApprovalWorkflowId", "StepOrder" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverPositionId",
            table: "ApprovalWorkflowSteps",
            column: "ApproverPositionId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverTeamId",
            table: "ApprovalWorkflowSteps",
            column: "ApproverTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverUserId",
            table: "ApprovalWorkflowSteps",
            column: "ApproverUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_CalendarId",
            table: "CalendarEventDeletionAuditLogs",
            column: "CalendarId");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_DeletedAt",
            table: "CalendarEventDeletionAuditLogs",
            column: "DeletedAt");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_DeletedByUserId",
            table: "CalendarEventDeletionAuditLogs",
            column: "DeletedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_EventId",
            table: "CalendarEventDeletionAuditLogs",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_Status",
            table: "CalendarEventDeletionAuditLogs",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventMetadata_CalendarId_EventId",
            table: "CalendarEventMetadata",
            columns: new[] { "CalendarId", "EventId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventMetadata_CreatedByUserId",
            table: "CalendarEventMetadata",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuItems_MenuKey",
            table: "NavigationMenuItems",
            column: "MenuKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuTeamPermissions_TeamId",
            table: "NavigationMenuTeamPermissions",
            column: "TeamId");

        migrationBuilder.CreateIndex(
            name: "IX_PositionPermissionGrants_PositionId_PermissionKey_ScopeType_TargetTeamId",
            table: "PositionPermissionGrants",
            columns: new[] { "PositionId", "PermissionKey", "ScopeType", "TargetTeamId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PositionPermissionGrants_TargetTeamId",
            table: "PositionPermissionGrants",
            column: "TargetTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_Positions_TeamId_Name",
            table: "Positions",
            columns: new[] { "TeamId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Teams_TeamKey",
            table: "Teams",
            column: "TeamKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TodoTaskMetadata_CreatedByUserId",
            table: "TodoTaskMetadata",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_TodoTaskMetadata_TaskListId_TaskId",
            table: "TodoTaskMetadata",
            columns: new[] { "TaskListId", "TaskId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FileId",
            table: "UploadAuditLogs",
            column: "FileId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FileId_UploadedAt",
            table: "UploadAuditLogs",
            columns: new[] { "FileId", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FolderId",
            table: "UploadAuditLogs",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FolderId_UploadedAt",
            table: "UploadAuditLogs",
            columns: new[] { "FolderId", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_PositionId",
            table: "UserAccounts",
            column: "PositionId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_TeamId",
            table: "UserAccounts",
            column: "TeamId");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_Username",
            table: "UserAccounts",
            column: "Username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApprovalAuditLogs");

        migrationBuilder.DropTable(
            name: "ApprovalSteps");

        migrationBuilder.DropTable(
            name: "ApprovalWorkflowSteps");

        migrationBuilder.DropTable(
            name: "CalendarEventDeletionAuditLogs");

        migrationBuilder.DropTable(
            name: "CalendarEventMetadata");

        migrationBuilder.DropTable(
            name: "GlobalSettings");

        migrationBuilder.DropTable(
            name: "GoogleStorageConnections");

        migrationBuilder.DropTable(
            name: "NavigationMenuTeamPermissions");

        migrationBuilder.DropTable(
            name: "PositionPermissionGrants");

        migrationBuilder.DropTable(
            name: "TodoTaskMetadata");

        migrationBuilder.DropTable(
            name: "UploadAuditLogs");

        migrationBuilder.DropTable(
            name: "ApprovalRequests");

        migrationBuilder.DropTable(
            name: "ApprovalWorkflows");

        migrationBuilder.DropTable(
            name: "NavigationMenuItems");

        migrationBuilder.DropTable(
            name: "UserAccounts");

        migrationBuilder.DropTable(
            name: "Positions");

        migrationBuilder.DropTable(
            name: "Teams");
    }
}
