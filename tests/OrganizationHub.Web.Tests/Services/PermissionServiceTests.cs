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

public sealed partial class PermissionServiceTests
{
    [Fact]
    public async Task TeamUserCanOnlyUseOwnTeamFoldersAndCommonReadFoldersAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(userSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(userSession, CancellationToken.None);

                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "public-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "common-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "work-resources-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-approved-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "management-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-approved-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "public-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
            }
        }
    }

    [Fact]
    public async Task TeamLeaderCanUseResponsibleSharedFolderAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession responsibleSession = createPlanningUserSession(EStoredCompatibilityRole.TeamLeader);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(responsibleSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(responsibleSession, CancellationToken.None);
                bool canReadResponsibleFile = await permissionService.CanReadFileAsync(responsibleSession, driveItemId("responsible-file"), CancellationToken.None);
                bool canUploadToResponsibleFolder = await permissionService.CanUploadToFolderAsync(responsibleSession, driveFolderId("responsible-folder"), CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.True(canReadResponsibleFile);
                Assert.True(canUploadToResponsibleFolder);
            }
        }
    }

    [Fact]
    public async Task TeamDeputyLeaderKeepsTeamMemberAuthorityAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession deputyLeaderSession = createPlanningUserSession(EStoredCompatibilityRole.TeamDeputyLeader);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(deputyLeaderSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(deputyLeaderSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> readableCalendars = await permissionService.GetReadableCalendarsAsync(deputyLeaderSession, CancellationToken.None);
                bool canUploadToResponsibleFolder = await permissionService.CanUploadToFolderAsync(deputyLeaderSession, driveFolderId("responsible-folder"), CancellationToken.None);
                bool canDeleteOwnTeamFile = await permissionService.CanDeleteInFolderAsync(deputyLeaderSession, driveFolderId("planning-child-folder"), CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.False(hasCalendar(readableCalendars, "responsible-calendar"));
                Assert.False(canUploadToResponsibleFolder);
                Assert.False(canDeleteOwnTeamFile);
            }
        }
    }

    [Fact]
    public async Task GeneralManagementReadsAllButUploadsOnlyManagementCommonAndWorkResourcesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession managementSession = createManagementSession(EStoredCompatibilityRole.ManagementStaff);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(managementSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(managementSession, CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "management-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "common-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "work-resources-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "public-folder");
            }
        }
    }

    [Fact]
    public async Task ManagementMaterialsFolderIsShownOnceWhenTeamAndGlobalFolderMatchAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                Team managementTeam = dbContext.Teams.Single(team => team.TeamKey == "Management");
                managementTeam.MaterialsFolderId = "management-folder";
                dbContext.SaveChanges();
            }

            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession directorSession = createManagementSession(EStoredCompatibilityRole.ManagementDirector);

                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(directorSession, CancellationToken.None);
                List<DriveFolderReference> managementFolders = uploadableFolders.Where(folder => folder.FolderId.GetRequiredValue() == "management-folder").ToList();

                DriveFolderReference managementFolder = Assert.Single(managementFolders);
                Assert.Equal("관리팀 자료", managementFolder.DisplayName);
                Assert.Equal(EDriveFolderKind.TeamMaterials, managementFolder.FolderKind);
                Assert.Equal(3, managementFolder.TeamIdValueOrNull);
                Assert.Equal("Management", managementFolder.TeamKey.GetRequiredValue());
                Assert.True(managementFolder.IsManagementTeam);
            }
        }
    }

    [Fact]
    public async Task DirectFileRequestsRespectTeamAndRootBoundariesAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

                bool canReadOwnFile = await permissionService.CanReadFileAsync(userSession, driveItemId("planning-materials-file"), CancellationToken.None);
                bool canReadCommonFile = await permissionService.CanReadFileAsync(userSession, driveItemId("common-file"), CancellationToken.None);
                bool canReadOtherTeamFile = await permissionService.CanReadFileAsync(userSession, driveItemId("operation-materials-file"), CancellationToken.None);
                bool canReadRootOutsideFile = await permissionService.CanReadFileAsync(userSession, driveItemId("outside-file"), CancellationToken.None);

                Assert.True(canReadOwnFile);
                Assert.False(canReadCommonFile);
                Assert.False(canReadOtherTeamFile);
                Assert.False(canReadRootOutsideFile);
            }
        }
    }

    [Fact]
    public async Task MissingOfficialRootClosesDriveAccessAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithoutOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(userSession, CancellationToken.None);
                bool canUpload = await permissionService.CanUploadToFolderAsync(userSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                bool canReadFile = await permissionService.CanReadFileAsync(userSession, driveItemId("planning-materials-file"), CancellationToken.None);

                Assert.Empty(readableFolders);
                Assert.False(canUpload);
                Assert.False(canReadFile);
            }
        }
    }

    [Fact]
    public async Task UploadPermissionAllowsChildFolderInsideOwnMaterialsRootAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

                bool canUploadToChildFolder = await permissionService.CanUploadToFolderAsync(userSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool canUploadToOtherTeamChildFolder = await permissionService.CanUploadToFolderAsync(userSession, driveFolderId("operation-child-folder"), CancellationToken.None);

                Assert.True(canUploadToChildFolder);
                Assert.False(canUploadToOtherTeamChildFolder);
            }
        }
    }

    [Fact]
    public async Task ConfiguredPositionGrantsOwnTeamUploadAndDeleteAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            UserSession userSession = await seedConfiguredPermissionUserAsync(dbContextFactory, 41, 1, EPermissionScopeType.OwnTeam, new List<string> { PermissionKeys.FILES_READ, PermissionKeys.FILES_UPLOAD, PermissionKeys.FILES_DELETE });
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(userSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(userSession, CancellationToken.None);
                bool canDeleteOwnTeamFile = await permissionService.CanDeleteInFolderAsync(userSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool canDeleteOtherTeamFile = await permissionService.CanDeleteInFolderAsync(userSession, driveFolderId("operation-child-folder"), CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.DoesNotContain(readableFolders, folder => folder.FolderId.GetRequiredValue() == "common-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.True(canDeleteOwnTeamFile);
                Assert.False(canDeleteOtherTeamFile);
            }
        }
    }

    [Fact]
    public async Task ApprovalDestinationAllowsChildFolderInsideApprovedRootOnlyAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);

                bool canStoreToApprovedChild = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-approved-child-folder"), CancellationToken.None);
                bool canStoreToMaterialsChild = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-child-folder"), CancellationToken.None);

                Assert.True(canStoreToApprovedChild);
                Assert.False(canStoreToMaterialsChild);
            }
        }
    }

}
