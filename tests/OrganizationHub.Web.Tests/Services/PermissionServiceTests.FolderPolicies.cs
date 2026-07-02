using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
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
    public async Task TeamMaterialsDeletePermissionSeparatesTeamAndManagementAuthorityAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession teamUserSession = createPlanningUserSession(EStoredCompatibilityRole.TeamUser);
                UserSession planningLeaderSession = createPlanningUserSession(EStoredCompatibilityRole.TeamLeader);
                UserSession operationLeaderSession = new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(4),
                        Username = Username.CreateOptional("operation.leader"),
                        DisplayName = DisplayName.CreateOptional("Operation Leader"),
                        StoredCompatibilityRole = EStoredCompatibilityRole.TeamLeader,
                        OrganizationTeamIdOrNull = OrganizationTeamId.CreateExisting(2),
                        TeamKey = new OrganizationTeamKey("Operation"),
                        TeamDisplayName = DisplayName.CreateOptional("운영팀")
                    });
                UserSession generalManagementSession = createManagementSession(EStoredCompatibilityRole.ManagementStaff);
                UserSession directorSession = createManagementSession(EStoredCompatibilityRole.ManagementDirector);
                UserSession adminSession = createManagementSession(EStoredCompatibilityRole.Admin);

                bool canTeamUserDeleteOwnTeam = await permissionService.CanDeleteInFolderAsync(teamUserSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool canPlanningLeaderDeleteOwnTeam = await permissionService.CanDeleteInFolderAsync(planningLeaderSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool canOperationLeaderDeletePlanningTeam = await permissionService.CanDeleteInFolderAsync(operationLeaderSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                bool canGeneralManagementDeleteTeam = await permissionService.CanDeleteInFolderAsync(generalManagementSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                bool canDirectorDeleteTeam = await permissionService.CanDeleteInFolderAsync(directorSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool canAdminDeleteTeam = await permissionService.CanDeleteInFolderAsync(adminSession, driveFolderId("operation-materials-folder"), CancellationToken.None);

                Assert.False(canTeamUserDeleteOwnTeam);
                Assert.True(canPlanningLeaderDeleteOwnTeam);
                Assert.False(canOperationLeaderDeletePlanningTeam);
                Assert.False(canGeneralManagementDeleteTeam);
                Assert.True(canDirectorDeleteTeam);
                Assert.True(canAdminDeleteTeam);
            }
        }
    }

    [Fact]
    public async Task FolderPermissionDecisionsExposeAllowedDeniedReasonAndScopeAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession planningLeaderSession = createPlanningUserSession(EStoredCompatibilityRole.TeamLeader);
                UserSession staffSession = createManagementSession(EStoredCompatibilityRole.ManagementStaff);

                PermissionDecision ownTeamDeleteDecision = await permissionService.DecideDeleteInFolderAsync(planningLeaderSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                PermissionDecision managementDeleteDecision = await permissionService.DecideDeleteInFolderAsync(staffSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                PermissionDecision publicUploadDecision = await permissionService.DecideUploadToFolderAsync(staffSession, driveFolderId("public-folder"), CancellationToken.None);

                Assert.True(ownTeamDeleteDecision.IsAllowed);
                Assert.Equal("기획팀 자료", ownTeamDeleteDecision.ScopeLabel);
                Assert.False(managementDeleteDecision.IsAllowed);
                Assert.Equal("기획팀 자료", managementDeleteDecision.ScopeLabel);
                Assert.Contains("삭제할 권한", managementDeleteDecision.Reason);
                Assert.True(publicUploadDecision.IsAllowed);
                Assert.Equal("외부 공유 자료", publicUploadDecision.ScopeLabel);
            }
        }
    }

    [Fact]
    public async Task SharedManagementFoldersUseManagementManagerDeletePermissionAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                string[] folderIds = new string[] { "management-folder", "public-folder", "common-folder", "work-resources-folder" };
                UserSession generalManagementSession = createManagementSession(EStoredCompatibilityRole.ManagementStaff);
                UserSession directorSession = createManagementSession(EStoredCompatibilityRole.ManagementDirector);
                UserSession deputyDirectorSession = createManagementSession(EStoredCompatibilityRole.ManagementDeputyDirector);
                UserSession managerSession = createManagementSession(EStoredCompatibilityRole.ManagementManager);
                UserSession adminSession = createManagementSession(EStoredCompatibilityRole.Admin);

                foreach (string folderId in folderIds)
                {
                    DriveFolderId targetFolderId = driveFolderId(folderId);
                    Assert.False(await permissionService.CanDeleteInFolderAsync(generalManagementSession, targetFolderId, CancellationToken.None));
                    Assert.True(await permissionService.CanDeleteInFolderAsync(directorSession, targetFolderId, CancellationToken.None));
                    Assert.True(await permissionService.CanDeleteInFolderAsync(deputyDirectorSession, targetFolderId, CancellationToken.None));
                    Assert.True(await permissionService.CanDeleteInFolderAsync(managerSession, targetFolderId, CancellationToken.None));
                    Assert.True(await permissionService.CanDeleteInFolderAsync(adminSession, targetFolderId, CancellationToken.None));
                }
            }
        }
    }

    [Fact]
    public async Task ApprovalDestinationFoldersDoNotExposeDeleteToManagementManagerAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession managerSession = createManagementSession(EStoredCompatibilityRole.ManagementManager);
                UserSession adminSession = createManagementSession(EStoredCompatibilityRole.Admin);

                bool canManagerDeleteApproved = await permissionService.CanDeleteInFolderAsync(managerSession, driveFolderId("planning-approved-folder"), CancellationToken.None);
                bool canAdminDeleteApproved = await permissionService.CanDeleteInFolderAsync(adminSession, driveFolderId("planning-approved-folder"), CancellationToken.None);

                Assert.True(canManagerDeleteApproved);
                Assert.True(canAdminDeleteApproved);
            }
        }
    }

    [Fact]
    public async Task ManagementCanUseManagementApprovedFolderAsApprovalDestinationAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache, new OrganizationPermissionService(dbContextFactory));
                UserSession userSession = new UserSession(
                    new UserSessionCreateRequest
                    {
                        UserAccountId = UserAccountId.CreateExisting(2),
                        Username = Username.CreateOptional("management.staff"),
                        DisplayName = DisplayName.CreateOptional("관리팀 담당자"),
                        StoredCompatibilityRole = EStoredCompatibilityRole.ManagementStaff,
                        OrganizationTeamIdOrNull = null,
                        TeamKey = OrganizationTeamKey.Empty,
                        TeamDisplayName = DisplayName.CreateOptional(string.Empty)
                    });

                bool canStoreToManagementFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("management-folder"), CancellationToken.None);
                bool canStoreToManagementApprovedFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("management-approved-folder"), CancellationToken.None);
                bool canStoreToPlanningApprovedFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-approved-folder"), CancellationToken.None);

                Assert.False(canStoreToManagementFolder);
                Assert.True(canStoreToManagementApprovedFolder);
                Assert.True(canStoreToPlanningApprovedFolder);
            }
        }
    }
}
