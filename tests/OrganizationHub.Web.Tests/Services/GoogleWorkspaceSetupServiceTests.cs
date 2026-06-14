using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class GoogleWorkspaceSetupServiceTests
{
    [Fact]
    public async Task ApplyStandardDriveFolderNamesRenamesMappedFoldersOnlyAsync()
    {
        using (TestDbContextFactory dbContextFactory = createConfiguredDbContextFactory())
        {
            FakeDriveService driveService = createConfiguredDriveService();
            GoogleWorkspaceSetupService workspaceSetupService = new GoogleWorkspaceSetupService(dbContextFactory, driveService);

            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result = await workspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            GoogleDriveFolderNameSyncSummary summary = getResultValue(result);
            Assert.Equal(8, summary.RenamedCount);
            Assert.Equal(0, summary.AlreadyStandardCount);
            Assert.Equal("Organization Hub", await getDriveItemNameAsync(driveService, "official-root"));
            Assert.Equal("팀별 자료", await getDriveItemNameAsync(driveService, "teams-root-folder"));
            Assert.Equal("외부 공유 자료", await getDriveItemNameAsync(driveService, "public-folder"));
            Assert.Equal("공통 자료", await getDriveItemNameAsync(driveService, "common-folder"));
            Assert.Equal("책임자 자료", await getDriveItemNameAsync(driveService, "responsible-folder"));
            Assert.Equal("업무 지원 자료", await getDriveItemNameAsync(driveService, "work-resources-folder"));
            Assert.Equal("기획팀", await getDriveItemNameAsync(driveService, "planning-team-folder"));
            Assert.Equal("자료", await getDriveItemNameAsync(driveService, "planning-materials-folder"));
            Assert.Equal("결재 완료 자료", await getDriveItemNameAsync(driveService, "planning-approved-folder"));
            Assert.Equal("Old Safety Materials", await getDriveItemNameAsync(driveService, "safety-materials-folder"));
        }
    }

    [Fact]
    public async Task ApplyStandardDriveFolderNamesTreatsSharedTeamParentAsTeamRootAsync()
    {
        using (TestDbContextFactory dbContextFactory = createFlatTeamDbContextFactory())
        {
            FakeDriveService driveService = createFlatTeamDriveService();
            GoogleWorkspaceSetupService workspaceSetupService = new GoogleWorkspaceSetupService(dbContextFactory, driveService);

            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result = await workspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            GoogleDriveFolderNameSyncSummary summary = getResultValue(result);
            DriveFileMetadata planningParentMetadata = await getDriveItemMetadataAsync(driveService, "folder-기획팀");
            DriveFileMetadata operationParentMetadata = await getDriveItemMetadataAsync(driveService, "folder-운영팀");
            DriveFileMetadata planningMaterialsMetadata = await getDriveItemMetadataAsync(driveService, "planning-materials-folder");
            DriveFileMetadata planningApprovedMetadata = await getDriveItemMetadataAsync(driveService, "planning-approved-folder");
            DriveFileMetadata operationMaterialsMetadata = await getDriveItemMetadataAsync(driveService, "operation-materials-folder");
            DriveFileMetadata operationApprovedMetadata = await getDriveItemMetadataAsync(driveService, "operation-approved-folder");
            Assert.Equal(2, summary.CreatedCount);
            Assert.Equal(4, summary.MovedCount);
            Assert.Equal("팀별 자료", await getDriveItemNameAsync(driveService, "teams-root-folder"));
            Assert.Equal("기획팀", planningParentMetadata.Name);
            Assert.Equal("운영팀", operationParentMetadata.Name);
            Assert.Equal("teams-root-folder", planningParentMetadata.ParentFolderIds[0]);
            Assert.Equal("teams-root-folder", operationParentMetadata.ParentFolderIds[0]);
            Assert.Equal("자료", planningMaterialsMetadata.Name);
            Assert.Equal("결재 완료 자료", planningApprovedMetadata.Name);
            Assert.Equal("자료", operationMaterialsMetadata.Name);
            Assert.Equal("결재 완료 자료", operationApprovedMetadata.Name);
            Assert.Equal("folder-기획팀", planningMaterialsMetadata.ParentFolderIds[0]);
            Assert.Equal("folder-기획팀", planningApprovedMetadata.ParentFolderIds[0]);
            Assert.Equal("folder-운영팀", operationMaterialsMetadata.ParentFolderIds[0]);
            Assert.Equal("folder-운영팀", operationApprovedMetadata.ParentFolderIds[0]);
        }
    }

    [Fact]
    public async Task ApplyStandardDriveFolderNamesKeepsManagementTeamFoldersInConfiguredPlaceAsync()
    {
        using (TestDbContextFactory dbContextFactory = createManagementApprovedDbContextFactory())
        {
            FakeDriveService driveService = createManagementApprovedDriveService();
            GoogleWorkspaceSetupService workspaceSetupService = new GoogleWorkspaceSetupService(dbContextFactory, driveService);

            ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result = await workspaceSetupService.ApplyStandardDriveFolderNamesAsync(CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            GoogleDriveFolderNameSyncSummary summary = getResultValue(result);
            DriveFileMetadata managementMaterialsMetadata = await getDriveItemMetadataAsync(driveService, "management-folder");
            DriveFileMetadata managementApprovedMetadata = await getDriveItemMetadataAsync(driveService, "management-approved-folder");
            Assert.Equal(0, summary.CreatedCount);
            Assert.Equal(0, summary.MovedCount);
            Assert.Equal("팀별 자료", await getDriveItemNameAsync(driveService, "teams-root-folder"));
            Assert.Equal("자료", managementMaterialsMetadata.Name);
            Assert.Equal("결재 완료 자료", managementApprovedMetadata.Name);
            Assert.Equal("official-root", managementMaterialsMetadata.ParentFolderIds[0]);
            Assert.Equal("official-root", managementApprovedMetadata.ParentFolderIds[0]);
        }
    }

    [Fact]
    public async Task EnsureStandardDriveFoldersCreatesReusableOrganizationStructureAsync()
    {
        using (TestDbContextFactory dbContextFactory = createWorkspaceProvisioningDbContextFactory())
        {
            FakeDriveService driveService = new FakeDriveService();
            GoogleWorkspaceSetupService workspaceSetupService = new GoogleWorkspaceSetupService(dbContextFactory, driveService);

            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> firstResult = await workspaceSetupService.EnsureStandardDriveFoldersAsync(CancellationToken.None);
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> secondResult = await workspaceSetupService.EnsureStandardDriveFoldersAsync(CancellationToken.None);

            Assert.True(firstResult.IsSuccess, firstResult.ErrorMessage);
            Assert.True(secondResult.IsSuccess, secondResult.ErrorMessage);
            GoogleWorkspaceResourceSetupSummary firstSummary = getWorkspaceSetupResultValue(firstResult);
            GoogleWorkspaceResourceSetupSummary secondSummary = getWorkspaceSetupResultValue(secondResult);
            Assert.Equal(14, firstSummary.CreatedCount);
            Assert.Equal(0, firstSummary.ReusedCount);
            Assert.Equal(11, firstSummary.AssignedCount);
            Assert.Equal(0, secondSummary.CreatedCount);
            Assert.Equal(14, secondSummary.ReusedCount);
            Assert.Equal(0, secondSummary.AssignedCount);
            using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
            {
                GlobalSetting setting = dbContext.GlobalSettings.Single();
                Team systemAdministrationTeam = dbContext.Teams.Single(team => team.TeamKey == OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY);
                Team managementTeam = dbContext.Teams.Single(team => team.TeamKey == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY);
                Team planningTeam = dbContext.Teams.Single(team => team.TeamKey == "Planning");
                Team operationTeam = dbContext.Teams.Single(team => team.TeamKey == "Operation");
                Assert.Equal("Organization Hub", await getDriveItemNameAsync(driveService, getRequiredId(setting.OfficialRootFolderId)));
                Assert.Equal("외부 공유 자료", await getDriveItemNameAsync(driveService, getRequiredId(setting.PublicFolderId)));
                Assert.Equal("공통 자료", await getDriveItemNameAsync(driveService, getRequiredId(setting.CommonFolderId)));
                Assert.Equal("책임자 자료", await getDriveItemNameAsync(driveService, getRequiredId(setting.ResponsibleResourcesFolderId)));
                Assert.Equal("업무 지원 자료", await getDriveItemNameAsync(driveService, getRequiredId(setting.WorkResourcesFolderId)));
                Assert.Equal("관리팀 자료", await getDriveItemNameAsync(driveService, getRequiredId(managementTeam.MaterialsFolderId)));
                Assert.Equal("관리팀 결재 완료 자료", await getDriveItemNameAsync(driveService, getRequiredId(managementTeam.ApprovedFolderId)));
                Assert.Equal("자료", await getDriveItemNameAsync(driveService, getRequiredId(planningTeam.MaterialsFolderId)));
                Assert.Equal("결재 완료 자료", await getDriveItemNameAsync(driveService, getRequiredId(planningTeam.ApprovedFolderId)));
                Assert.Equal("자료", await getDriveItemNameAsync(driveService, getRequiredId(operationTeam.MaterialsFolderId)));
                Assert.Equal("결재 완료 자료", await getDriveItemNameAsync(driveService, getRequiredId(operationTeam.ApprovedFolderId)));
                Assert.True(string.IsNullOrWhiteSpace(systemAdministrationTeam.MaterialsFolderId));
                Assert.True(string.IsNullOrWhiteSpace(systemAdministrationTeam.ApprovedFolderId));
            }
        }
    }

    private static TestDbContextFactory createConfiguredDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.OfficialRootFolderId = "official-root";
            setting.PublicFolderId = "public-folder";
            setting.CommonFolderId = "common-folder";
            setting.ResponsibleResourcesFolderId = "responsible-folder";
            setting.WorkResourcesFolderId = "work-resources-folder";
            setting.ArchiveFolderId = "archive-folder";
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", EActivationState.Active));
            dbContext.Teams.Add(createTeam(2, "Safety", "안전관리팀", "safety-materials-folder", "safety-approved-folder", EActivationState.Inactive));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static TestDbContextFactory createWorkspaceProvisioningDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(createTeam(1, OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY, OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME, string.Empty, string.Empty, EActivationState.Active));
            dbContext.Teams.Add(createTeam(2, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME, string.Empty, string.Empty, EActivationState.Active));
            dbContext.Teams.Add(createTeam(3, "Planning", "기획팀", string.Empty, string.Empty, EActivationState.Active));
            dbContext.Teams.Add(createTeam(4, "Operation", "운영팀", string.Empty, string.Empty, EActivationState.Active));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static TestDbContextFactory createFlatTeamDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.OfficialRootFolderId = "official-root";
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(createTeam(1, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", EActivationState.Active));
            dbContext.Teams.Add(createTeam(2, "Operation", "운영팀", "operation-materials-folder", "operation-approved-folder", EActivationState.Active));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static TestDbContextFactory createManagementApprovedDbContextFactory()
    {
        TestDbContextFactory dbContextFactory = new TestDbContextFactory();
        using (OrganizationHubDbContext dbContext = dbContextFactory.CreateDbContext())
        {
            GlobalSetting setting = new GlobalSetting();
            setting.Id = 1;
            setting.OfficialRootFolderId = "official-root";
            dbContext.GlobalSettings.Add(setting);
            dbContext.Teams.Add(createTeam(1, "Management", "관리팀", "management-folder", "management-approved-folder", EActivationState.Active));
            dbContext.Teams.Add(createTeam(2, "Planning", "기획팀", "planning-materials-folder", "planning-approved-folder", EActivationState.Active));
            dbContext.SaveChanges();
        }

        return dbContextFactory;
    }

    private static Team createTeam(int id, string teamKey, string displayName, string materialsFolderId, string approvedFolderId, EActivationState activationState)
    {
        Team team = new Team();
        team.Id = id;
        team.TeamKey = teamKey;
        team.DisplayName = displayName;
        team.MaterialsFolderId = materialsFolderId;
        team.ApprovedFolderId = approvedFolderId;
        team.IsActive = activationState == EActivationState.Active;
        return team;
    }

    private static FakeDriveService createConfiguredDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("official-root", "Organization Hub");
        driveService.AddFolder("teams-root-folder", "03_Teams", "official-root");
        driveService.AddFolder("public-folder", "00_Public", "official-root");
        driveService.AddFolder("common-folder", "01_Common", "official-root");
        driveService.AddFolder("management-folder", "02_Management", "official-root");
        driveService.AddFolder("responsible-folder", "Responsible", "official-root");
        driveService.AddFolder("work-resources-folder", "04_WorkResources", "official-root");
        driveService.AddFolder("archive-folder", "05_Archive", "official-root");
        driveService.AddFolder("planning-team-folder", "Planning", "teams-root-folder");
        driveService.AddFolder("safety-team-folder", "Safety", "teams-root-folder");
        driveService.AddFolder("planning-materials-folder", "Materials", "planning-team-folder");
        driveService.AddFolder("planning-approved-folder", "Approved", "planning-team-folder");
        driveService.AddFolder("safety-materials-folder", "Old Safety Materials", "safety-team-folder");
        driveService.AddFolder("safety-approved-folder", "Old Safety Approved", "safety-team-folder");
        return driveService;
    }

    private static FakeDriveService createFlatTeamDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("official-root", "Organization Hub");
        driveService.AddFolder("teams-root-folder", "03_Teams", "official-root");
        driveService.AddFolder("planning-materials-folder", "Materials", "teams-root-folder");
        driveService.AddFolder("planning-approved-folder", "Approved", "teams-root-folder");
        driveService.AddFolder("operation-materials-folder", "Operation Materials", "teams-root-folder");
        driveService.AddFolder("operation-approved-folder", "Operation Approved", "teams-root-folder");
        return driveService;
    }

    private static FakeDriveService createManagementApprovedDriveService()
    {
        FakeDriveService driveService = new FakeDriveService();
        driveService.AddFolder("official-root", "Organization Hub");
        driveService.AddFolder("teams-root-folder", "03_Teams", "official-root");
        driveService.AddFolder("planning-team-folder", "Planning", "teams-root-folder");
        driveService.AddFolder("planning-materials-folder", "Materials", "planning-team-folder");
        driveService.AddFolder("planning-approved-folder", "Approved", "planning-team-folder");
        driveService.AddFolder("management-folder", "02_Management", "official-root");
        driveService.AddFolder("management-approved-folder", "관리팀 결재 완료 자료", "official-root");
        return driveService;
    }

    private static GoogleDriveFolderNameSyncSummary getResultValue(ServiceResultOfT<GoogleDriveFolderNameSyncSummary> result)
    {
        if (result.ValueOrNull == null)
        {
            throw new InvalidOperationException("Expected a result value.");
        }

        return result.ValueOrNull;
    }

    private static GoogleWorkspaceResourceSetupSummary getWorkspaceSetupResultValue(ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> result)
    {
        if (result.ValueOrNull == null)
        {
            throw new InvalidOperationException("Expected a result value.");
        }

        return result.ValueOrNull;
    }

    private static string getRequiredId(string? idOrNull)
    {
        if (string.IsNullOrWhiteSpace(idOrNull))
        {
            throw new InvalidOperationException("Expected a configured Id.");
        }

        return idOrNull;
    }

    private static async Task<string> getDriveItemNameAsync(FakeDriveService driveService, string itemId)
    {
        DriveFileMetadata metadata = await getDriveItemMetadataAsync(driveService, itemId);
        return metadata.Name;
    }

    private static async Task<DriveFileMetadata> getDriveItemMetadataAsync(FakeDriveService driveService, string itemId)
    {
        DriveFileMetadata? metadataOrNull = await driveService.GetFileOrNullAsync(DriveItemId.CreateOptional(itemId), CancellationToken.None);
        if (metadataOrNull == null)
        {
            throw new InvalidOperationException("Expected a Drive item.");
        }

        return metadataOrNull;
    }
}
