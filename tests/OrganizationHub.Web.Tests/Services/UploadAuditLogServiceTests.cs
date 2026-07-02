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

public sealed partial class UploadAuditLogServiceTests
{
    [Fact]
    public async Task RecordUploadStoresUploaderAffiliationSnapshotAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference planningRoot = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);
                DriveFileItem uploadedFile = new DriveFileItem();
                uploadedFile.Id = "new-planning-file";
                uploadedFile.Name = "new-planning-file.pdf";

                await uploadAuditLogService.RecordUploadAsync(userSession, planningRoot, uploadedFile, CancellationToken.None);

                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    UploadAuditLog storedAuditLog = dbContext.UploadAuditLogs.Single(auditLog => auditLog.FileId == "new-planning-file");
                    Assert.Equal(1, storedAuditLog.UploaderTeamId);
                    Assert.Equal("기획팀", storedAuditLog.UploaderTeamName);
                    Assert.Equal("기획팀 자료 담당", storedAuditLog.UploaderPositionName);
                }
            }
        }
    }

    [Fact]
    public async Task RecordUploadStoresNestedFolderDisplayNameAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference monthlyFolder = createTeamFolderReference(
                    "monthly-operation-folder",
                    "기획팀 자료 / 월간 운영 자료",
                    1,
                    "기획팀",
                    EDriveFolderUploadAccess.UploadAllowed);
                DriveFileItem uploadedFile = new DriveFileItem();
                uploadedFile.Id = "monthly-operation-minutes";
                uploadedFile.Name = "월간 운영 회의록.txt";

                await uploadAuditLogService.RecordUploadAsync(userSession, monthlyFolder, uploadedFile, CancellationToken.None);

                using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
                {
                    UploadAuditLog storedAuditLog = dbContext.UploadAuditLogs.Single(auditLog => auditLog.FileId == "monthly-operation-minutes");
                    Assert.Equal("monthly-operation-folder", storedAuditLog.FolderId);
                    Assert.Equal("기획팀 자료 / 월간 운영 자료", storedAuditLog.FolderDisplayName);
                }
            }
        }
    }

    [Fact]
    public async Task RecentUploadsForRootStaysWithinSelectedReadableRootAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference planningRoot = createTeamFolderReference("planning-materials-folder", "기획팀 자료", 1, "기획팀", EDriveFolderUploadAccess.UploadAllowed);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, planningRoot, new RecentItemLimit(10), CancellationToken.None);

                Assert.Equal(new string[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileId).ToArray());
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();
                DriveFolderReference operationRoot = createTeamFolderReference("operation-materials-folder", "운영팀 자료", 2, "운영팀", EDriveFolderUploadAccess.UploadDenied);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, operationRoot, new RecentItemLimit(10), CancellationToken.None);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsAsync(userSession, new RecentItemLimit(10), CancellationToken.None);

                Assert.Contains(recentUploads, item => item.FileId == "planning-root-file");
                Assert.DoesNotContain(recentUploads, item => item.FileId == "operation-root-file");
                Assert.DoesNotContain(recentUploads, item => item.FileId == "outside-root-file");
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createManagementSession();
                DriveFolderReference managementRoot = createTeamFolderReference("management-folder", "관리팀 자료", 3, new OrganizationTeamKey("Management"), "관리팀", EDriveFolderUploadAccess.UploadAllowed);

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForRootAsync(userSession, managementRoot, new RecentItemLimit(10), CancellationToken.None);

                Assert.Equal(new string[] { "management-child-file", "management-root-file" }, recentUploads.Select(item => item.FileId).ToArray());
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, new RecentItemLimit(10), EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.Equal(new string[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileId).ToArray());
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createManagementSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, new RecentItemLimit(10), EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.Equal(new string[] { "operation-root-file", "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileId).ToArray());
                Assert.DoesNotContain(recentUploads, item => item.FileId == "management-child-file");
                Assert.DoesNotContain(recentUploads, item => item.FileId == "same-team-outside-file");
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, new RecentItemLimit(10), EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.DoesNotContain(recentUploads, item => item.FileId == "deleted-planning-file");
                Assert.Equal(new string[] { "planning-child-file", "planning-root-file" }, recentUploads.Select(item => item.FileId).ToArray());
            }
        }
    }

    [Fact]
    public async Task RecentUploadSnapshotsForReadableRootsIncludeAuditRecordBeforeDriveValidationAsync()
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UploadAuditLogService uploadAuditLogService = new UploadAuditLogService(dbContextFactory, permissionService, driveService);
                UserSession userSession = createPlanningUserSession();

                IReadOnlyList<UploadAuditLogItem> recentUploads = await uploadAuditLogService.GetRecentUploadSnapshotsForReadableRootsAsync(userSession, EDriveFolderKind.TeamMaterials, new RecentItemLimit(10), EManagementTeamFilter.ExcludeManagementTeam, CancellationToken.None);

                Assert.Equal("deleted-planning-file", recentUploads[0].FileId);
                Assert.Contains(recentUploads, item => item.FileId == "planning-child-file");
            }
        }
    }
}
