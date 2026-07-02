using System;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Tests.Fakes;
using OrganizationHub.Web.Tests.Infrastructure;

namespace OrganizationHub.Web.Tests.Services;

public sealed partial class GoogleWorkspaceSetupServiceTests
{
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
