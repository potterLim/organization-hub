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
                assignIfChanged(setting.OfficialRootFolderId, officialRoot.Id, value => setting.OfficialRootFolderId = value, summary);
                DriveFileItem publicFolder = await getOrCreateDriveFolderAsync(officialRoot.Id, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.PublicFolderId, publicFolder.Id, value => setting.PublicFolderId = value, summary);
                DriveFileItem commonFolder = await getOrCreateDriveFolderAsync(officialRoot.Id, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.CommonFolderId, commonFolder.Id, value => setting.CommonFolderId = value, summary);
                DriveFileItem responsibleResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.Id, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.ResponsibleResourcesFolderId, responsibleResourcesFolder.Id, value => setting.ResponsibleResourcesFolderId = value, summary);
                DriveFileItem workResourcesFolder = await getOrCreateDriveFolderAsync(officialRoot.Id, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, summary, cancellationToken);
                assignIfChanged(setting.WorkResourcesFolderId, workResourcesFolder.Id, value => setting.WorkResourcesFolderId = value, summary);
                DriveFileItem teamRootFolder = await getOrCreateDriveFolderAsync(officialRoot.Id, OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME, summary, cancellationToken);
                List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
                foreach (Team team in teams)
                {
                    await ensureTeamDriveFoldersAsync(team, officialRoot.Id, teamRootFolder.Id, summary, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                clearDriveFolderCaches(officialRoot.Id, teamRootFolder.Id);
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

    private async Task ensureTeamDriveFoldersAsync(
        Team team,
        string officialRootFolderId,
        string teamRootFolderId,
        GoogleWorkspaceResourceSetupSummary summary,
        CancellationToken cancellationToken
    )
    {
        if (new OrganizationTeamKey(team.TeamKey).IsManagementTeamKey)
        {
            DriveFileItem managementFolder = await getOrCreateDriveFolderAsync(officialRootFolderId, OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.MaterialsFolderId, managementFolder.Id, value => team.MaterialsFolderId = value, summary);
            DriveFileItem managementApprovedFolder = await getOrCreateDriveFolderAsync(officialRootFolderId, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
            assignIfChanged(team.ApprovedFolderId, managementApprovedFolder.Id, value => team.ApprovedFolderId = value, summary);
            return;
        }

        DriveFileItem teamFolder = await getOrCreateDriveFolderAsync(teamRootFolderId, team.DisplayName, summary, cancellationToken);
        DriveFileItem materialsFolder = await getOrCreateDriveFolderAsync(teamFolder.Id, OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.MaterialsFolderId, materialsFolder.Id, value => team.MaterialsFolderId = value, summary);
        DriveFileItem approvedFolder = await getOrCreateDriveFolderAsync(teamFolder.Id, OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, cancellationToken);
        assignIfChanged(team.ApprovedFolderId, approvedFolder.Id, value => team.ApprovedFolderId = value, summary);
    }

    private async Task<DriveFileItem> getOrCreateDriveFolderAsync(
        string parentFolderId,
        string folderName,
        GoogleWorkspaceResourceSetupSummary summary,
        CancellationToken cancellationToken
    )
    {
        summary.TargetCount = summary.TargetCount + 1;
        DriveFolderId parentFolder = DriveFolderId.CreateOptional(parentFolderId);
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolder, cancellationToken);
        DriveFileItem? existingFolderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (existingFolderOrNull != null)
        {
            summary.ReusedCount = summary.ReusedCount + 1;
            return existingFolderOrNull;
        }

        DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(parentFolder, folderName, cancellationToken);
        summary.CreatedCount = summary.CreatedCount + 1;
        return createdFolder;
    }

    private void assignIfChanged(
        string? currentValueOrNull,
        string nextValue,
        Action<string> assignAction,
        GoogleWorkspaceResourceSetupSummary summary
    )
    {
        if (string.Equals(getTextOrEmpty(currentValueOrNull), nextValue, StringComparison.Ordinal))
        {
            return;
        }

        assignAction(nextValue);
        summary.AssignedCount = summary.AssignedCount + 1;
    }

    private void clearDriveFolderCaches(params string[] folderIds)
    {
        if (mMemoryCacheOrNull == null)
        {
            return;
        }

        mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + "root");
        foreach (string folderId in folderIds)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                continue;
            }

            mMemoryCacheOrNull.Remove(DRIVE_FOLDER_CACHE_PREFIX + folderId);
        }
    }
}
