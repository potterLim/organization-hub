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
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CalendarID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CalendarName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                EventID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventSummary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                EventLocation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                EventEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                DeletedByUserID = table.Column<int>(type: "INTEGER", nullable: false),
                DeletedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                DeletedByRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                DeletedByTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                DeletedByTeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEventDeletionAuditLogs", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "CalendarEventMetadata",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CalendarID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                EventID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedByUserID = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CalendarEventMetadata", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "GlobalSettings",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                OfficialRootFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                PublicFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CommonFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleResourcesFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                WorkResourcesFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ArchiveFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                OrganizationCalendarID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleCalendarID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                OrganizationTaskListID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ResponsibleTaskListID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                NoticeFileID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                WorkGuidelineFileID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ShowTodoAuthor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                ShowCalendarAuthor = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GlobalSettings", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "GoogleStorageConnections",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
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
                table.PrimaryKey("PK_GoogleStorageConnections", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "NavigationMenuItems",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
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
                table.PrimaryKey("PK_NavigationMenuItems", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "Teams",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TeamKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                MaterialsFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                ApprovedFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CalendarID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                TaskListID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "TodoTaskMetadata",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TaskListID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                TaskID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedByUserID = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TodoTaskMetadata", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "UploadAuditLogs",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FileID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                FolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FolderDisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                FolderKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                UploaderUserID = table.Column<int>(type: "INTEGER", nullable: false),
                UploaderDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                UploaderRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                TeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadAuditLogs", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalWorkflows",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                AppliesToTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalWorkflows", x => x.ID);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflows_Teams_AppliesToTeamID",
                    column: x => x.AppliesToTeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NavigationMenuTeamPermissions",
            columns: table => new
            {
                NavigationMenuItemID = table.Column<int>(type: "INTEGER", nullable: false),
                TeamID = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NavigationMenuTeamPermissions", x => new { x.NavigationMenuItemID, x.TeamID });
                table.ForeignKey(
                    name: "FK_NavigationMenuTeamPermissions_NavigationMenuItems_NavigationMenuItemID",
                    column: x => x.NavigationMenuItemID,
                    principalTable: "NavigationMenuItems",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_NavigationMenuTeamPermissions_Teams_TeamID",
                    column: x => x.TeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Positions",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSystemAdministrator = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Positions", x => x.ID);
                table.ForeignKey(
                    name: "FK_Positions_Teams_TeamID",
                    column: x => x.TeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PositionPermissionGrants",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PositionID = table.Column<int>(type: "INTEGER", nullable: false),
                PermissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TargetTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                IsAllowed = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PositionPermissionGrants", x => x.ID);
                table.ForeignKey(
                    name: "FK_PositionPermissionGrants_Positions_PositionID",
                    column: x => x.PositionID,
                    principalTable: "Positions",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PositionPermissionGrants_Teams_TargetTeamID",
                    column: x => x.TargetTeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserAccounts",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                PositionID = table.Column<int>(type: "INTEGER", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAccounts", x => x.ID);
                table.ForeignKey(
                    name: "FK_UserAccounts_Positions_PositionID",
                    column: x => x.PositionID,
                    principalTable: "Positions",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserAccounts_Teams_TeamID",
                    column: x => x.TeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRequests",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Title = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                FileID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                MimeType = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                SourceFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                SourceFolderKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                TeamName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                RequesterUserID = table.Column<int>(type: "INTEGER", nullable: false),
                RequesterDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StepCount = table.Column<int>(type: "INTEGER", nullable: false),
                CurrentStepNumber = table.Column<int>(type: "INTEGER", nullable: false),
                ApprovedFileID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ApprovedFolderID = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ApprovedFolderDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                RejectedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRequests", x => x.ID);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_Teams_TeamID",
                    column: x => x.TeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_UserAccounts_RequesterUserID",
                    column: x => x.RequesterUserID,
                    principalTable: "UserAccounts",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalWorkflowSteps",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalWorkflowID = table.Column<int>(type: "INTEGER", nullable: false),
                StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                StepName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ApproverSelectionType = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                ApproverUserID = table.Column<int>(type: "INTEGER", nullable: true),
                ApproverPositionID = table.Column<int>(type: "INTEGER", nullable: true),
                ApproverTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                RequiredPermissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ScopeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                IsRequired = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalWorkflowSteps", x => x.ID);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_ApprovalWorkflows_ApprovalWorkflowID",
                    column: x => x.ApprovalWorkflowID,
                    principalTable: "ApprovalWorkflows",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_Positions_ApproverPositionID",
                    column: x => x.ApproverPositionID,
                    principalTable: "Positions",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_Teams_ApproverTeamID",
                    column: x => x.ApproverTeamID,
                    principalTable: "Teams",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ApprovalWorkflowSteps_UserAccounts_ApproverUserID",
                    column: x => x.ApproverUserID,
                    principalTable: "UserAccounts",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalAuditLogs",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalRequestID = table.Column<int>(type: "INTEGER", nullable: false),
                ActorUserID = table.Column<int>(type: "INTEGER", nullable: false),
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
                table.PrimaryKey("PK_ApprovalAuditLogs", x => x.ID);
                table.ForeignKey(
                    name: "FK_ApprovalAuditLogs_ApprovalRequests_ApprovalRequestID",
                    column: x => x.ApprovalRequestID,
                    principalTable: "ApprovalRequests",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalSteps",
            columns: table => new
            {
                ID = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ApprovalRequestID = table.Column<int>(type: "INTEGER", nullable: false),
                StepNumber = table.Column<int>(type: "INTEGER", nullable: false),
                ApproverUserID = table.Column<int>(type: "INTEGER", nullable: false),
                ApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ActionedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalSteps", x => x.ID);
                table.ForeignKey(
                    name: "FK_ApprovalSteps_ApprovalRequests_ApprovalRequestID",
                    column: x => x.ApprovalRequestID,
                    principalTable: "ApprovalRequests",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApprovalSteps_UserAccounts_ApproverUserID",
                    column: x => x.ApproverUserID,
                    principalTable: "UserAccounts",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_Action",
            table: "ApprovalAuditLogs",
            column: "Action");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_ActorUserID",
            table: "ApprovalAuditLogs",
            column: "ActorUserID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_ApprovalRequestID",
            table: "ApprovalAuditLogs",
            column: "ApprovalRequestID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalAuditLogs_CreatedAt",
            table: "ApprovalAuditLogs",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_ApprovedFileID",
            table: "ApprovalRequests",
            column: "ApprovedFileID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_FileID",
            table: "ApprovalRequests",
            column: "FileID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_RequesterUserID",
            table: "ApprovalRequests",
            column: "RequesterUserID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_Status",
            table: "ApprovalRequests",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_TeamID",
            table: "ApprovalRequests",
            column: "TeamID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_ApprovalRequestID_StepNumber",
            table: "ApprovalSteps",
            columns: new[] { "ApprovalRequestID", "StepNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_ApproverUserID",
            table: "ApprovalSteps",
            column: "ApproverUserID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalSteps_Status",
            table: "ApprovalSteps",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflows_AppliesToTeamID_Priority",
            table: "ApprovalWorkflows",
            columns: new[] { "AppliesToTeamID", "Priority" });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApprovalWorkflowID_StepOrder",
            table: "ApprovalWorkflowSteps",
            columns: new[] { "ApprovalWorkflowID", "StepOrder" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverPositionID",
            table: "ApprovalWorkflowSteps",
            column: "ApproverPositionID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverTeamID",
            table: "ApprovalWorkflowSteps",
            column: "ApproverTeamID");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalWorkflowSteps_ApproverUserID",
            table: "ApprovalWorkflowSteps",
            column: "ApproverUserID");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_CalendarID",
            table: "CalendarEventDeletionAuditLogs",
            column: "CalendarID");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_DeletedAt",
            table: "CalendarEventDeletionAuditLogs",
            column: "DeletedAt");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_DeletedByUserID",
            table: "CalendarEventDeletionAuditLogs",
            column: "DeletedByUserID");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_EventID",
            table: "CalendarEventDeletionAuditLogs",
            column: "EventID");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventDeletionAuditLogs_Status",
            table: "CalendarEventDeletionAuditLogs",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventMetadata_CalendarID_EventID",
            table: "CalendarEventMetadata",
            columns: new[] { "CalendarID", "EventID" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CalendarEventMetadata_CreatedByUserID",
            table: "CalendarEventMetadata",
            column: "CreatedByUserID");

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuItems_MenuKey",
            table: "NavigationMenuItems",
            column: "MenuKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NavigationMenuTeamPermissions_TeamID",
            table: "NavigationMenuTeamPermissions",
            column: "TeamID");

        migrationBuilder.CreateIndex(
            name: "IX_PositionPermissionGrants_PositionID_PermissionKey_ScopeType_TargetTeamID",
            table: "PositionPermissionGrants",
            columns: new[] { "PositionID", "PermissionKey", "ScopeType", "TargetTeamID" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PositionPermissionGrants_TargetTeamID",
            table: "PositionPermissionGrants",
            column: "TargetTeamID");

        migrationBuilder.CreateIndex(
            name: "IX_Positions_TeamID_Name",
            table: "Positions",
            columns: new[] { "TeamID", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Teams_TeamKey",
            table: "Teams",
            column: "TeamKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TodoTaskMetadata_CreatedByUserID",
            table: "TodoTaskMetadata",
            column: "CreatedByUserID");

        migrationBuilder.CreateIndex(
            name: "IX_TodoTaskMetadata_TaskListID_TaskID",
            table: "TodoTaskMetadata",
            columns: new[] { "TaskListID", "TaskID" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FileID",
            table: "UploadAuditLogs",
            column: "FileID");

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FileID_UploadedAt",
            table: "UploadAuditLogs",
            columns: new[] { "FileID", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FolderID",
            table: "UploadAuditLogs",
            column: "FolderID");

        migrationBuilder.CreateIndex(
            name: "IX_UploadAuditLogs_FolderID_UploadedAt",
            table: "UploadAuditLogs",
            columns: new[] { "FolderID", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_PositionID",
            table: "UserAccounts",
            column: "PositionID");

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_TeamID",
            table: "UserAccounts",
            column: "TeamID");

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
