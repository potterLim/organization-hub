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

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task TeamUserCanOnlyUseOwnTeamFoldersAndCommonReadFoldersAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = createPlanningUserSession(EUserRole.TeamUser);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(userSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(userSession, CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "public-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "common-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "work-resources-folder");
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession responsibleSession = createPlanningUserSession(EUserRole.TeamLeader);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession deputyLeaderSession = createPlanningUserSession(EUserRole.TeamDeputyLeader);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession managementSession = createManagementSession(EUserRole.ManagementStaff);

                IReadOnlyList<DriveFolderReference> readableFolders = await permissionService.GetReadableDriveFoldersAsync(managementSession, CancellationToken.None);
                IReadOnlyList<DriveFolderReference> uploadableFolders = await permissionService.GetUploadableDriveFoldersAsync(managementSession, CancellationToken.None);

                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.Contains(readableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "management-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "common-folder");
                Assert.Contains(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "work-resources-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "planning-materials-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "operation-materials-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "responsible-folder");
                Assert.DoesNotContain(uploadableFolders, folder => folder.FolderId.GetRequiredValue() == "public-folder");
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession directorSession = createManagementSession(EUserRole.ManagementDirector);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = createPlanningUserSession(EUserRole.TeamUser);

                bool canReadOwnFile = await permissionService.CanReadFileAsync(userSession, driveItemId("planning-materials-file"), CancellationToken.None);
                bool canReadCommonFile = await permissionService.CanReadFileAsync(userSession, driveItemId("common-file"), CancellationToken.None);
                bool canReadOtherTeamFile = await permissionService.CanReadFileAsync(userSession, driveItemId("operation-materials-file"), CancellationToken.None);
                bool canReadRootOutsideFile = await permissionService.CanReadFileAsync(userSession, driveItemId("outside-file"), CancellationToken.None);

                Assert.True(canReadOwnFile);
                Assert.True(canReadCommonFile);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = createPlanningUserSession(EUserRole.TeamUser);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = createPlanningUserSession(EUserRole.TeamUser);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = createPlanningUserSession(EUserRole.TeamUser);

                bool canStoreToApprovedChild = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-approved-child-folder"), CancellationToken.None);
                bool canStoreToMaterialsChild = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-child-folder"), CancellationToken.None);

                Assert.True(canStoreToApprovedChild);
                Assert.False(canStoreToMaterialsChild);
            }
        }
    }

    [Fact]
    public async Task TeamMaterialsDeletePermissionSeparatesTeamAndManagementAuthorityAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession teamUserSession = createPlanningUserSession(EUserRole.TeamUser);
                UserSession planningLeaderSession = createPlanningUserSession(EUserRole.TeamLeader);
                UserSession operationLeaderSession = new UserSession(UserAccountId.CreateExisting(4), "operation.leader", "Operation Leader", EUserRole.TeamLeader, OrganizationTeamId.CreateExisting(2), new OrganizationTeamKey("Operation"), "운영팀");
                UserSession generalManagementSession = createManagementSession(EUserRole.ManagementStaff);
                UserSession directorSession = createManagementSession(EUserRole.ManagementDirector);
                UserSession adminSession = createManagementSession(EUserRole.Admin);

                bool teamUserCanDeleteOwnTeam = await permissionService.CanDeleteInFolderAsync(teamUserSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool planningLeaderCanDeleteOwnTeam = await permissionService.CanDeleteInFolderAsync(planningLeaderSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool operationLeaderCanDeletePlanningTeam = await permissionService.CanDeleteInFolderAsync(operationLeaderSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                bool generalManagementCanDeleteTeam = await permissionService.CanDeleteInFolderAsync(generalManagementSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                bool directorCanDeleteTeam = await permissionService.CanDeleteInFolderAsync(directorSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                bool adminCanDeleteTeam = await permissionService.CanDeleteInFolderAsync(adminSession, driveFolderId("operation-materials-folder"), CancellationToken.None);

                Assert.False(teamUserCanDeleteOwnTeam);
                Assert.True(planningLeaderCanDeleteOwnTeam);
                Assert.False(operationLeaderCanDeletePlanningTeam);
                Assert.False(generalManagementCanDeleteTeam);
                Assert.True(directorCanDeleteTeam);
                Assert.True(adminCanDeleteTeam);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession planningLeaderSession = createPlanningUserSession(EUserRole.TeamLeader);
                UserSession staffSession = createManagementSession(EUserRole.ManagementStaff);

                PermissionDecision ownTeamDeleteDecision = await permissionService.DecideDeleteInFolderAsync(planningLeaderSession, driveFolderId("planning-child-folder"), CancellationToken.None);
                PermissionDecision managementDeleteDecision = await permissionService.DecideDeleteInFolderAsync(staffSession, driveFolderId("planning-materials-folder"), CancellationToken.None);
                PermissionDecision publicUploadDecision = await permissionService.DecideUploadToFolderAsync(staffSession, driveFolderId("public-folder"), CancellationToken.None);

                Assert.True(ownTeamDeleteDecision.IsAllowed);
                Assert.Equal("자기 팀 자료", ownTeamDeleteDecision.ScopeLabel);
                Assert.False(managementDeleteDecision.IsAllowed);
                Assert.Equal("팀 자료", managementDeleteDecision.ScopeLabel);
                Assert.Contains("해당 팀 책임자", managementDeleteDecision.Reason);
                Assert.False(publicUploadDecision.IsAllowed);
                Assert.Equal("파일 올리기", publicUploadDecision.ScopeLabel);
                Assert.Contains("파일을 올릴 권한", publicUploadDecision.Reason);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                string[] folderIds = new[] { "management-folder", "public-folder", "common-folder", "work-resources-folder" };
                UserSession generalManagementSession = createManagementSession(EUserRole.ManagementStaff);
                UserSession directorSession = createManagementSession(EUserRole.ManagementDirector);
                UserSession deputyDirectorSession = createManagementSession(EUserRole.ManagementDeputyDirector);
                UserSession managerSession = createManagementSession(EUserRole.ManagementManager);
                UserSession adminSession = createManagementSession(EUserRole.Admin);

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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession managerSession = createManagementSession(EUserRole.ManagementManager);
                UserSession adminSession = createManagementSession(EUserRole.Admin);

                bool managerCanDeleteApproved = await permissionService.CanDeleteInFolderAsync(managerSession, driveFolderId("planning-approved-folder"), CancellationToken.None);
                bool adminCanDeleteApproved = await permissionService.CanDeleteInFolderAsync(adminSession, driveFolderId("planning-approved-folder"), CancellationToken.None);

                Assert.False(managerCanDeleteApproved);
                Assert.True(adminCanDeleteApproved);
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
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession userSession = new UserSession(UserAccountId.CreateExisting(2), "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, OrganizationTeamKey.Empty, string.Empty);

                bool canStoreToManagementFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("management-folder"), CancellationToken.None);
                bool canStoreToManagementApprovedFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("management-approved-folder"), CancellationToken.None);
                bool canStoreToPlanningApprovedFolder = await permissionService.CanStoreApprovedFileToFolderAsync(userSession, driveFolderId("planning-approved-folder"), CancellationToken.None);

                Assert.False(canStoreToManagementFolder);
                Assert.True(canStoreToManagementApprovedFolder);
                Assert.False(canStoreToPlanningApprovedFolder);
            }
        }
    }

    [Fact]
    public async Task CalendarPolicySeparatesTeamMemberLeaderAndManagementAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);
                UserSession teamUserSession = createPlanningUserSession(EUserRole.TeamUser);
                UserSession responsibleSession = createPlanningUserSession(EUserRole.TeamLeader);
                UserSession managementSession = new UserSession(UserAccountId.CreateExisting(2), "management.staff", "관리팀 담당자", EUserRole.ManagementStaff, null, OrganizationTeamKey.Empty, string.Empty);
                UserSession directorSession = new UserSession(UserAccountId.CreateExisting(3), "director", "Director", EUserRole.ManagementDirector, null, OrganizationTeamKey.Empty, string.Empty);

                IReadOnlyList<CalendarReference> teamUserReadableCalendars = await permissionService.GetReadableCalendarsAsync(teamUserSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> leaderReadableCalendars = await permissionService.GetReadableCalendarsAsync(responsibleSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> leaderWritableCalendars = await permissionService.GetWritableCalendarsAsync(responsibleSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> managementReadableCalendars = await permissionService.GetReadableCalendarsAsync(managementSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> managementWritableCalendars = await permissionService.GetWritableCalendarsAsync(managementSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> directorWritableCalendars = await permissionService.GetWritableCalendarsAsync(directorSession, CancellationToken.None);

                Assert.True(hasCalendar(teamUserReadableCalendars, "all"));
                Assert.Equal("전체", getCalendarDisplayName(teamUserReadableCalendars, "all"));
                Assert.True(hasCalendar(teamUserReadableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(teamUserReadableCalendars, "responsible-calendar"));
                Assert.False(hasCalendar(teamUserReadableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(leaderReadableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(leaderWritableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(leaderWritableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(leaderWritableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "management-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(managementReadableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(managementWritableCalendars, "management-calendar"));
                Assert.False(hasCalendar(managementWritableCalendars, "responsible-calendar"));
                Assert.False(hasCalendar(managementWritableCalendars, "operation-calendar"));
                Assert.True(hasCalendar(directorWritableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(directorWritableCalendars, "operation-calendar"));
            }
        }
    }

    [Fact]
    public async Task ConfiguredPositionGrantsOwnTeamCalendarReadAndWriteAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory(EOfficialRootConfiguration.WithOfficialRoot))
        {
            UserSession userSession = await seedConfiguredPermissionUserAsync(dbContextFactory, 42, 1, EPermissionScopeType.OwnTeam, new List<string> { PermissionKeys.CALENDAR_READ, PermissionKeys.CALENDAR_CREATE });
            using (MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions()))
            {
                FakeDriveService driveService = createConfiguredDriveService();
                PermissionService permissionService = new PermissionService(dbContextFactory, driveService, memoryCache);

                IReadOnlyList<CalendarReference> readableCalendars = await permissionService.GetReadableCalendarsAsync(userSession, CancellationToken.None);
                IReadOnlyList<CalendarReference> writableCalendars = await permissionService.GetWritableCalendarsAsync(userSession, CancellationToken.None);
                bool canWritePlanningCalendar = await permissionService.CanWriteCalendarAsync(userSession, calendarId("planning-calendar"), CancellationToken.None);
                bool canWriteOperationCalendar = await permissionService.CanWriteCalendarAsync(userSession, calendarId("operation-calendar"), CancellationToken.None);

                Assert.True(hasCalendar(readableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(readableCalendars, "operation-calendar"));
                Assert.False(hasCalendar(readableCalendars, "responsible-calendar"));
                Assert.True(hasCalendar(writableCalendars, "planning-calendar"));
                Assert.False(hasCalendar(writableCalendars, "operation-calendar"));
                Assert.True(canWritePlanningCalendar);
                Assert.False(canWriteOperationCalendar);
            }
        }
    }

    private static TestDbContextFactory createConfiguredDbContextFactory(EOfficialRootConfiguration officialRootConfiguration)
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
            setting.ArchiveFolderId = "archive-folder";
            setting.OrganizationCalendarId = "management-calendar";
            setting.ResponsibleCalendarId = "responsible-calendar";
            if (officialRootConfiguration == EOfficialRootConfiguration.WithOfficialRoot)
            {
                setting.OfficialRootFolderId = "official-root";
            }

            Team planningTeam = createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", "planning-calendar", 10);
            Team operationTeam = createTeam(2, "Operation", "운영팀", "operation-materials-folder", "operation-approved-folder", "operation-calendar", 20);
            Team managementTeam = createTeam(3, "Management", "관리팀", "management-folder", "management-approved-folder", string.Empty, 0);
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(managementTeam);
            dbContext.Teams.Add(planningTeam);
            dbContext.Teams.Add(operationTeam);
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string materialsFolderId, string approvedFolderId, string calendarId, int sortOrder)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.MaterialsFolderId = materialsFolderId;
        team.ApprovedFolderId = approvedFolderId;
        team.CalendarId = calendarId;
        team.SortOrder = sortOrder;
        team.IsActive = true;
        return team;
    }

    private static FakeDriveService createConfiguredDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("public-folder", "외부 배포자료", "official-root");
        driveService.AddFolder("common-folder", "공통 자료", "official-root");
        driveService.AddFolder("management-folder", "관리팀 자료", "official-root");
        driveService.AddFolder("management-approved-folder", "관리팀 결재 완료 자료", "official-root");
        driveService.AddFolder("responsible-folder", "책임자 자료", "official-root");
        driveService.AddFolder("work-resources-folder", "업무 지원 자료", "official-root");
        driveService.AddFolder("archive-folder", "Archived Materials", "official-root");
        driveService.AddFolder("planning-team-folder", "기획팀", "official-root");
        driveService.AddFolder("operation-team-folder", "운영팀", "official-root");
        driveService.AddFolder("planning-materials-folder", "기획팀 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-folder", "기획팀 결재 완료 자료", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-child-folder", "회의록", "planning-materials-folder", "planning-team-folder", "official-root");
        driveService.AddFolder("planning-approved-child-folder", "예산", "planning-approved-folder", "planning-team-folder", "official-root");
        driveService.AddFolder("operation-materials-folder", "운영팀 자료", "operation-team-folder", "official-root");
        driveService.AddFolder("operation-approved-folder", "운영팀 결재 완료 자료", "operation-team-folder", "official-root");
        driveService.AddFolder("operation-child-folder", "회의록", "operation-materials-folder", "operation-team-folder", "official-root");
        driveService.AddFile("planning-materials-file", "2026-05-21_기획팀_회의록.pdf", "application/pdf", "planning-materials-folder", "planning-team-folder", "official-root");
        driveService.AddFile("operation-materials-file", "2026-05-21_운영팀_회의록.pdf", "application/pdf", "operation-materials-folder", "operation-team-folder", "official-root");
        driveService.AddFile("common-file", "공통양식.pdf", "application/pdf", "common-folder", "official-root");
        driveService.AddFile("responsible-file", "책임자공유.pdf", "application/pdf", "responsible-folder", "official-root");
        driveService.AddFile("outside-file", "외부자료.pdf", "application/pdf", "outside-folder");
        return driveService;
    }

    private static async Task<UserSession> seedConfiguredPermissionUserAsync(TestDbContextFactory dbContextFactory, int userId, int teamId, EPermissionScopeType scopeType, IReadOnlyList<string> permissionKeys)
    {
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            OrganizationPosition position = new OrganizationPosition();
            position.TeamId = teamId;
            position.Name = "자료 담당";
            position.Description = string.Empty;
            position.SortOrder = 10;
            position.IsActive = true;
            dbContext.Positions.Add(position);

            foreach (string permissionKey in permissionKeys)
            {
                PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
                permissionGrant.Position = position;
                permissionGrant.PermissionKey = permissionKey;
                permissionGrant.ScopeType = scopeType;
                permissionGrant.IsAllowed = true;
                dbContext.PositionPermissionGrants.Add(permissionGrant);
            }

            UserAccount userAccount = new UserAccount();
            userAccount.Id = userId;
            userAccount.Username = "configured.user";
            userAccount.DisplayName = "권한 설정 사용자";
            userAccount.PasswordHash = "test";
            userAccount.Role = EUserRole.TeamUser;
            userAccount.TeamId = teamId;
            userAccount.Position = position;
            userAccount.IsActive = true;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new UserSession(UserAccountId.CreateExisting(userAccount.Id), userAccount.Username, userAccount.DisplayName, userAccount.Role, OrganizationTeamId.CreateExisting(teamId), new OrganizationTeamKey("Planning"), "기획팀");
        }
    }

    private static UserSession createPlanningUserSession(EUserRole role)
    {
        return new UserSession(UserAccountId.CreateExisting(1), "planning", "Planning User", role, OrganizationTeamId.CreateExisting(1), new OrganizationTeamKey("Planning"), "기획팀");
    }

    private static UserSession createManagementSession(EUserRole role)
    {
        return new UserSession(UserAccountId.CreateExisting(2), "hq", "Management", role, null, OrganizationTeamKey.Empty, string.Empty);
    }

    private static DriveItemId driveItemId(string value)
    {
        return DriveItemId.CreateOptional(value);
    }

    private static DriveFolderId driveFolderId(string value)
    {
        return DriveFolderId.CreateOptional(value);
    }

    private static CalendarId calendarId(string value)
    {
        return CalendarId.CreateOptional(value);
    }

    private static bool hasCalendar(IReadOnlyList<CalendarReference> calendars, string calendarId)
    {
        CalendarReference? calendarOrNull = calendars.FirstOrDefault(calendar => calendar.CalendarId.GetRequiredValue() == calendarId);
        return calendarOrNull != null;
    }

    private static string getCalendarDisplayName(IReadOnlyList<CalendarReference> calendars, string calendarId)
    {
        CalendarReference? calendarOrNull = calendars.FirstOrDefault(calendar => calendar.CalendarId.GetRequiredValue() == calendarId);
        if (calendarOrNull == null)
        {
            return string.Empty;
        }

        return calendarOrNull.DisplayName;
    }

    private enum EOfficialRootConfiguration
    {
        WithoutOfficialRoot,
        WithOfficialRoot
    }
}
