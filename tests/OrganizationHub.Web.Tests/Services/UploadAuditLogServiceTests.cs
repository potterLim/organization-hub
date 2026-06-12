using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class UploadAuditLogServiceTests
{
    [Fact]
    public async Task RecentUploadsForRootStaysWithinSelectedReadableRootAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference planningRoot = new DriveFolderReference("planning-materials-folder", "기획팀 자료", EDriveFolderKind.TeamMaterials, 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, planningRoot, 10, CancellationToken.None);

                Assert.Equal(new[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileID).ToArray());
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForRootReturnsEmptyForUnreadableRootAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference operationRoot = new DriveFolderReference("operation-materials-folder", "운영팀 자료", EDriveFolderKind.TeamMaterials, 2, "운영팀", EDriveFolderUploadAccess.UploadDenied);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, operationRoot, 10, CancellationToken.None);

                Assert.Empty(recentUploads);
            }
        }
    }

    [Fact]
    public async Task RecentUploadsOnlyIncludesReadableRootFoldersAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsAsync(userSession, 10, CancellationToken.None);

                Assert.Contains(recentUploads, item => item.FileID == "planning-root-file");
                Assert.DoesNotContain(recentUploads, item => item.FileID == "operation-root-file");
                Assert.DoesNotContain(recentUploads, item => item.FileID == "outside-root-file");
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForManagementTeamRootIncludesTeamScopedManagementLogsAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createManagementSession();
                DriveFolderReference managementRoot = new DriveFolderReference("management-folder", "관리팀 자료", EDriveFolderKind.TeamMaterials, 3, "Management", "관리팀", EDriveFolderUploadAccess.UploadAllowed);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, managementRoot, 10, CancellationToken.None);

                Assert.Equal(new[] { "management-child-file", "management-root-file" }, recentUploads.Select(item => item.FileID).ToArray());
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForReadableRootsIncludesNestedReadableTeamFilesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, 10, EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.Equal(new[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileID).ToArray());
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForReadableRootsHandlesMultiTeamAccessWithoutLeakingManagementOrOutsideRootFilesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createManagementSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, 10, EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.Equal(new[] { "operation-root-file", "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileID).ToArray());
                Assert.DoesNotContain(recentUploads, item => item.FileID == "management-child-file");
                Assert.DoesNotContain(recentUploads, item => item.FileID == "same-team-outside-file");
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForReadableRootsSkipsDeletedFilesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                dbContext.UploadAuditLogs.Add(createAuditLog("deleted-planning-file", "planning-child-folder", EDriveFolderKind.TeamMaterials, 1, "기획팀", DateTime.UtcNow.AddMinutes(10)));
                dbContext.SaveChanges();
            }

            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, 10, EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.DoesNotContain(recentUploads, item => item.FileID == "deleted-planning-file");
                Assert.Equal(new[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileID).ToArray());
            }
        }
    }

    private static TestDbContextFactory createConfiguredDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.ID = 1;
            setting.PublicFolderID = "public-folder";
            setting.CommonFolderID = "common-folder";
            setting.ResponsibleResourcesFolderID = "responsible-folder";
            setting.WorkResourcesFolderID = "work-resources-folder";
            setting.OfficialRootFolderID = "official-root";

            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-materials-folder", "operation-approved-folder", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", "management-folder", "management-approved-folder", 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);

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

    private static Team createTeam(int id, string teamKey, string displayName, string materialsFolderID, string approvedFolderID, int sortOrder)
    {
        Team team = new Team();
        team.ID = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.MaterialsFolderID = materialsFolderID;
        team.ApprovedFolderID = approvedFolderID;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static UploadAuditLog createAuditLog(string fileID, string folderID, EDriveFolderKind folderKind, int? teamID, string teamName, DateTime uploadedAt)
    {
        UploadAuditLog auditLog = new UploadAuditLog();
        auditLog.FileID = fileID;
        auditLog.FileName = fileID + ".pdf";
        auditLog.FolderID = folderID;
        auditLog.FolderDisplayName = folderID;
        auditLog.FolderKind = folderKind;
        auditLog.UploaderUserID = 1;
        auditLog.UploaderDisplayName = "기획팀원";
        auditLog.UploaderRole = EUserRole.TeamUser;
        auditLog.TeamID = teamID;
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
        return new UserSession(1, "planning", "Planning User", EUserRole.TeamUser, 1, "Planning", "기획팀");
    }

    private static UserSession createManagementSession()
    {
        return new UserSession(2, "director", "Director", EUserRole.ManagementDirector, null, string.Empty, string.Empty);
    }
}
