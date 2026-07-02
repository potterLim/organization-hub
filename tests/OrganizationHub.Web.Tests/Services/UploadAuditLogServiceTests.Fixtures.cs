using System;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class UploadAuditLogServiceTests
{
    private static TestDbContextFactory createConfiguredDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.PublicFolderId = "public-folder";
            setting.CommonFolderId = "common-folder";
            setting.ResponsibleResourcesFolderId = "responsible-folder";
            setting.WorkResourcesFolderId = "work-resources-folder";
            setting.OfficialRootFolderId = "official-root";

            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-materials-folder", "operation-approved-folder", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", "management-folder", "management-approved-folder", 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);
            seedPermissionUsers(dbContext);

            DateTime uploadedAt = DateTime.UtcNow;
            dbContext.UploadAuditLogs.Add(createAuditLog("management-child-file", "management-child-folder", EDriveFolderKind.TeamMaterials, 3, "관리팀", uploadedAt.AddMinutes(4)));
            dbContext.UploadAuditLogs.Add(createAuditLog("management-root-file", "management-folder", EDriveFolderKind.TeamMaterials, 3, "관리팀", uploadedAt.AddMinutes(3)));
            dbContext.UploadAuditLogs.Add(createAuditLog("planning-child-file", "planning-child-folder", EDriveFolderKind.TeamMaterials, 1, "기획팀", uploadedAt));
            dbContext.UploadAuditLogs.Add(createAuditLog("planning-root-file", "planning-materials-folder", EDriveFolderKind.TeamMaterials, 1, "기획팀", uploadedAt.AddMinutes(-1)));
            dbContext.UploadAuditLogs.Add(createAuditLog("operation-root-file", "operation-materials-folder", EDriveFolderKind.TeamMaterials, 2, "운영팀", uploadedAt.AddMinutes(1)));
            dbContext.UploadAuditLogs.Add(createAuditLog("same-team-outside-file", "rogue-folder", EDriveFolderKind.TeamMaterials, 1, "기획팀", uploadedAt.AddMinutes(2)));
            dbContext.UploadAuditLogs.Add(createAuditLog("outside-root-file", "outside-folder", EDriveFolderKind.Common, null, string.Empty, uploadedAt.AddMinutes(3)));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static void seedPermissionUsers(OrganizationHubDbContext dbContext)
    {
        OrganizationPosition planningPosition = createPosition(1, "기획팀 자료 담당", 10);
        addPermissionGrant(planningPosition, PermissionKeys.FILES_READ, EPermissionScopeType.OwnTeam);

        OrganizationPosition managementPosition = createPosition(3, "관리팀 자료 담당", 10);
        addPermissionGrant(managementPosition, PermissionKeys.FILES_READ, EPermissionScopeType.Global);

        dbContext.Positions.Add(planningPosition);
        dbContext.Positions.Add(managementPosition);
        dbContext.UserAccounts.Add(createUser(1, "planning.member", "기획팀원", EStoredCompatibilityRole.TeamUser, 1, planningPosition));
        dbContext.UserAccounts.Add(createUser(2, "director", "Director", EStoredCompatibilityRole.ManagementDirector, 3, managementPosition));
    }

    private static OrganizationPosition createPosition(int teamId, string name, int sortOrder)
    {
        OrganizationPosition position = new OrganizationPosition();
        position.TeamId = teamId;
        position.Name = name;
        position.Description = string.Empty;
        position.SortOrder = sortOrder;
        position.IsActive = true;
        return position;
    }

    private static void addPermissionGrant(OrganizationPosition position, string permissionKey, EPermissionScopeType scopeType)
    {
        PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
        permissionGrant.Position = position;
        permissionGrant.PermissionKey = permissionKey;
        permissionGrant.ScopeType = scopeType;
        permissionGrant.IsAllowed = true;
        position.PermissionGrants.Add(permissionGrant);
    }

    private static UserAccount createUser(int id, string username, string displayName, EStoredCompatibilityRole role, int teamId, OrganizationPosition position)
    {
        UserAccount userAccount = new UserAccount();
        userAccount.Id = id;
        userAccount.Username = username;
        userAccount.DisplayName = displayName;
        userAccount.PasswordHash = "test";
        userAccount.StoredCompatibilityRole = role;
        userAccount.TeamId = teamId;
        userAccount.Position = position;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string materialsFolderId, string approvedFolderId, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.MaterialsFolderId = materialsFolderId;
        team.ApprovedFolderId = approvedFolderId;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static DriveFolderReference createTeamFolderReference(string folderId, string displayName, int teamId, string teamName, EDriveFolderUploadAccess uploadAccess)
    {
        return createTeamFolderReference(folderId, displayName, teamId, OrganizationTeamKey.Empty, teamName, uploadAccess);
    }

    private static DriveFolderReference createTeamFolderReference(string folderId, string displayName, int teamId, OrganizationTeamKey teamKey, string teamName, EDriveFolderUploadAccess uploadAccess)
    {
        DriveFolderOwnerContext ownerContext = new DriveFolderOwnerContext(OrganizationTeamId.CreateExisting(teamId), teamKey, teamName);
        DriveFolderReferenceCreationRequest request = new DriveFolderReferenceCreationRequest(DriveFolderId.CreateOptional(folderId), displayName, EDriveFolderKind.TeamMaterials, uploadAccess, ownerContext);
        return new DriveFolderReference(request);
    }

    private static UploadAuditLog createAuditLog(string fileId, string folderId, EDriveFolderKind folderKind, int? teamId, string teamName, DateTime uploadedAt)
    {
        UploadAuditLog auditLog = new UploadAuditLog();
        auditLog.FileId = fileId;
        auditLog.FileName = fileId + ".pdf";
        auditLog.FolderId = folderId;
        auditLog.FolderDisplayName = folderId;
        auditLog.FolderKind = folderKind;
        auditLog.UploaderUserId = 1;
        auditLog.UploaderDisplayName = "기획팀원";
        auditLog.UploaderStoredCompatibilityRole = EStoredCompatibilityRole.TeamUser;
        auditLog.UploaderPositionId = 1;
        auditLog.UploaderPositionName = "기획팀 자료 담당";
        auditLog.TeamId = teamId;
        auditLog.TeamName = teamName;
        auditLog.UploadedAt = uploadedAt;
        return auditLog;
    }

    private static FakeDriveService createConfiguredDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("public-folder", "외부 배포자료", "official-root");
        driveService.AddFolder("common-folder", "공통 자료", "official-root");
        driveService.AddFolder("management-folder", "관리팀 자료", "official-root");
        driveService.AddFolder("management-child-folder", "관리팀 하위 폴더", "management-folder", "official-root");
        driveService.AddFolder("management-approved-folder", "관리팀 결재 완료 자료", "official-root");
        driveService.AddFolder("responsible-folder", "책임자 자료", "official-root");
        driveService.AddFolder("work-resources-folder", "업무 지원 자료", "official-root");
        driveService.AddFolder("planning-materials-folder", "기획팀 자료", "official-root");
        driveService.AddFolder("planning-child-folder", "기획 하위 폴더", "planning-materials-folder", "official-root");
        driveService.AddFolder("planning-approved-folder", "기획팀 결재 완료 자료", "official-root");
        driveService.AddFolder("operation-materials-folder", "운영팀 자료", "official-root");
        driveService.AddFolder("operation-approved-folder", "운영팀 결재 완료 자료", "official-root");
        driveService.AddFolder("rogue-folder", "잘못된 폴더", "outside-root");
        driveService.AddFolder("outside-folder", "외부 폴더", "outside-root");
        driveService.AddFile("management-child-file", "management-child-file.pdf", "application/pdf", "management-child-folder", "management-folder", "official-root");
        driveService.AddFile("management-root-file", "management-root-file.pdf", "application/pdf", "management-folder", "official-root");
        driveService.AddFile("planning-child-file", "planning-child-file.pdf", "application/pdf", "planning-child-folder", "planning-materials-folder", "official-root");
        driveService.AddFile("planning-root-file", "planning-root-file.pdf", "application/pdf", "planning-materials-folder", "official-root");
        driveService.AddFile("operation-root-file", "operation-root-file.pdf", "application/pdf", "operation-materials-folder", "official-root");
        driveService.AddFile("same-team-outside-file", "same-team-outside-file.pdf", "application/pdf", "rogue-folder", "outside-root");
        return driveService;
    }

    private static UserSession createPlanningUserSession()
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(1),
                Username = Username.CreateOptional("planning"),
                DisplayName = DisplayName.CreateOptional("Planning User"),
                StoredCompatibilityRole = EStoredCompatibilityRole.TeamUser,
                OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(1),
                TeamKey = new OrganizationTeamKey("Planning"),
                TeamDisplayName = DisplayName.CreateOptional("기획팀")
            });
    }

    private static UserSession createManagementSession()
    {
        return new UserSession(
            new UserSessionCreateRequest
            {
                UserAccountId = UserAccountId.CreateExisting(2),
                Username = Username.CreateOptional("director"),
                DisplayName = DisplayName.CreateOptional("Director"),
                StoredCompatibilityRole = EStoredCompatibilityRole.ManagementDirector,
                OrganizationTeamIdOrNull = null,
                TeamKey = OrganizationTeamKey.Empty,
                TeamDisplayName = DisplayName.CreateOptional(string.Empty)
            });
    }
}
