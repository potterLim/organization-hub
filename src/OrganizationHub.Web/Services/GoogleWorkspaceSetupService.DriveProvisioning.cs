using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    public async Task<ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>> EnsureStandardDriveFoldersAsync(CancellationToken cancellationToken)
    {
        GoogleWorkspaceResourceSetupSummary summary = new GoogleWorkspaceResourceSetupSummary();
        List<string> failureMessages = new List<string>();
        try
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
                DriveFileItem officialRoot = await getOrCreateDriveFolderAsync("root", OrganizationHubProduct.NAME, summary, cancellationToken);
                assignIfChanged(setting.OfficialRootFolderID, officialRoot.ID, value => setting.OfficialRootFolderID = value, summary);
                DriveFileItem publicFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.PublicFolderID, publicFolder.ID, value => setting.PublicFolderID = value, summary);
                DriveFileItem commonFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.CommonFolderID, commonFolder.ID, value => setting.CommonFolderID = value, summary);
                DriveFileItem responsibleResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.ResponsibleResourcesFolderID, responsibleResourcesFolder.ID, value => setting.ResponsibleResourcesFolderID = value, summary);
                DriveFileItem workResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.WorkResourcesFolderID, workResourcesFolder.ID, value => setting.WorkResourcesFolderID = value, summary);
                DriveFileItem teamRootFolder = await getOrCreateDriveFolderAsync(officialRoot.ID, OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME, summary, cancellationToken);
                List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
                foreach (Team team in teams)
                {
                    await ensureTeamDriveFoldersAsync(team, officialRoot.ID, teamRootFolder.ID, summary, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                clearDriveFolderCaches(officialRoot.ID, teamRootFolder.ID);
            }
        }
        catch (Exception exception)
        {
            failureMessages.Add("기본 Drive 폴더를 준비하지 못했습니다. " + exception.Message);
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Fail(createResourceSetupFailureMessage("Drive 폴더", failureMessages));
        }

        return ServiceResultOfT<GoogleWorkspaceResourceSetupSummary>.Succeed(summary);
    }

    private async Task ensureTeamDriveFoldersAsync(Team team, string officialRootFolderID, string teamRootFolderID, GoogleWorkspaceResourceSetupSummary summary, CancellationToken cancellationToken)
    {
        if (team.TeamKey == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY)
        {
            DriveFileItem managementFolder = await getOrCreateDriveFolderAsync(officialRootFolderID, OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.MaterialsFolderID, managementFolder.ID, value => team.MaterialsFolderID = value, summary);
            DriveFileItem managementApprovedFolder = await getOrCreateDriveFolderAsync(officialRootFolderID, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.ApprovedFolderID, managementApprovedFolder.ID, value => team.ApprovedFolderID = value, summary);
            return;
        }

        DriveFileItem teamFolder = await getOrCreateDriveFolderAsync(teamRootFolderID, team.DisplayName, summary, cancellationToken);
        DriveFileItem materialsFolder = await getOrCreateDriveFolderAsync(teamFolder.ID, OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.MaterialsFolderID, materialsFolder.ID, value => team.MaterialsFolderID = value, summary);
        DriveFileItem approvedFolder = await getOrCreateDriveFolderAsync(teamFolder.ID, OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.ApprovedFolderID, approvedFolder.ID, value => team.ApprovedFolderID = value, summary);
    }

    private async Task<DriveFileItem> getOrCreateDriveFolderAsync(string parentFolderID, string folderName, GoogleWorkspaceResourceSetupSummary summary, CancellationToken cancellationToken)
    {
        summary.TargetCount = summary.TargetCount + 1;
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolderID, cancellationToken);
        DriveFileItem? existingFolderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (existingFolderOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingFolderOrNull;
        }

        DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(parentFolderID, folderName, cancellationToken);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdFolder;
    }

    private void assignIfChanged(string? currentValueOrNull, string nextValue, Action<string> assignAction, GoogleWorkspaceResourceSetupSummary summary)
    {
        if (string.Equals(getNullAsEmpty(currentValueOrNull), nextValue, StringComparison.Ordinal))
        {
            return;
        }

        assignAction(nextValue);
        summary.AssignedCount = summary.AssignedCount + 1;
    }

    private void clearDriveFolderCaches(params string[] folderIDs)
    {
        if (mMemoryCacheOrNull == null)
        {
            return;
        }

        mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + "root");
        foreach (string folderID in folderIDs)
        {
            if (string.IsNullOrWhiteSpace(folderID))
            {
                continue;
            }

            mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + folderID);
        }
    }
}
