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
    public async Task<ServiceResultOfT<GoogleDriveFolderNameSyncSummary>> ApplyStandardDriveFolderNamesAsync(CancellationToken cancellationToken)
    {
        GoogleDriveFolderNameSyncSummary summary = new GoogleDriveFolderNameSyncSummary();
        List<string> failureMessages = new List<string>();
        HashSet<string> appliedFolderIDs = new HashSet<string>();
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting setting = await getGlobalSettingAsync(dbContext, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.PublicFolderID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.CommonFolderID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.ResponsibleResourcesFolderID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            await applyFolderNameIfConfiguredAsync(setting.WorkResourcesFolderID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            List<Team> teams = await getWorkspaceTeamsAsync(dbContext, cancellationToken);
            await applyTeamFolderNamesAsync(setting, teams, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }

        if (failureMessages.Count > 0)
        {
            return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Fail(createFolderNameFailureMessage(failureMessages));
        }

        if (summary.TargetCount == 0)
        {
            return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Fail("표준 이름을 적용할 Drive 폴더가 아직 설정되지 않았습니다.");
        }

        return ServiceResultOfT<GoogleDriveFolderNameSyncSummary>.Succeed(summary);
    }

    private async Task applyTeamFolderNamesAsync(GlobalSetting setting, IReadOnlyList<Team> teams, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        List<GoogleWorkspaceTeamFolderNameContext> teamFolderNameContexts = new List<GoogleWorkspaceTeamFolderNameContext>();
        Dictionary<string, int> parentFolderUsageCounts = new Dictionary<string, int>();
        HashSet<string> teamRootCandidateFolderIDs = new HashSet<string>();
        foreach (Team team in teams)
        {
            DriveFileMetadata? materialsMetadataOrNull = await applyFolderNameIfConfiguredAsync(team.MaterialsFolderID, OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            DriveFileMetadata? approvedMetadataOrNull = await applyFolderNameIfConfiguredAsync(team.ApprovedFolderID, OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
            IReadOnlySet<string> teamParentFolderIDs = getSharedParentFolderIDs(materialsMetadataOrNull, approvedMetadataOrNull, setting.OfficialRootFolderID);
            if (teamParentFolderIDs.Count != 1)
            {
                continue;
            }

            string teamParentFolderID = teamParentFolderIDs.First();
            teamFolderNameContexts.Add(new GoogleWorkspaceTeamFolderNameContext(team, teamParentFolderID, materialsMetadataOrNull, approvedMetadataOrNull));
            if (parentFolderUsageCounts.ContainsKey(teamParentFolderID))
            {
                parentFolderUsageCounts[teamParentFolderID] = parentFolderUsageCounts[teamParentFolderID] + 1;
            }
            else
            {
                parentFolderUsageCounts[teamParentFolderID] = 1;
            }
        }

        foreach (GoogleWorkspaceTeamFolderNameContext teamFolderNameContext in teamFolderNameContexts)
        {
            if (parentFolderUsageCounts[teamFolderNameContext.TeamParentFolderID] > 1)
            {
                teamRootCandidateFolderIDs.Add(teamFolderNameContext.TeamParentFolderID);
                DriveFileMetadata? nestedTeamParentMetadataOrNull = await getOrCreateTeamParentFolderAsync(teamFolderNameContext.TeamParentFolderID, teamFolderNameContext.Team.DisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
                if (nestedTeamParentMetadataOrNull != null)
                {
                    await moveFolderIfNeededAsync(teamFolderNameContext.MaterialsMetadataOrNull, nestedTeamParentMetadataOrNull.ID, teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME, summary, failureMessages, cancellationToken);
                    await moveFolderIfNeededAsync(teamFolderNameContext.ApprovedMetadataOrNull, nestedTeamParentMetadataOrNull.ID, teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME, summary, failureMessages, cancellationToken);
                }

                continue;
            }

            DriveFileMetadata? teamParentMetadataOrNull = await applyFolderNameByIDAsync(teamFolderNameContext.TeamParentFolderID, teamFolderNameContext.Team.DisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
            if (teamParentMetadataOrNull == null)
            {
                continue;
            }

            foreach (string parentFolderID in teamParentMetadataOrNull.ParentFolderIDs)
            {
                if (isOfficialRootFolderID(parentFolderID, setting.OfficialRootFolderID) == false)
                {
                    teamRootCandidateFolderIDs.Add(parentFolderID);
                }
            }
        }

        if (teamRootCandidateFolderIDs.Count == 1)
        {
            string teamRootFolderID = teamRootCandidateFolderIDs.First();
            await applyFolderNameByIDAsync(teamRootFolderID, OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }
    }

    private async Task<DriveFileMetadata?> getOrCreateTeamParentFolderAsync(string teamRootFolderID, string teamDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        DriveFileMetadata? childFolderOrNull = await getChildFolderByNameOrNullAsync(teamRootFolderID, teamDisplayName, cancellationToken);
        if (childFolderOrNull != null)
        {
            return await applyFolderNameByIDAsync(childFolderOrNull.ID, teamDisplayName, summary, failureMessages, appliedFolderIDs, cancellationToken);
        }

        return await createTeamParentFolderAsync(teamRootFolderID, teamDisplayName, summary, failureMessages, cancellationToken);
    }

    private async Task<DriveFileMetadata?> createTeamParentFolderAsync(string teamRootFolderID, string teamDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, CancellationToken cancellationToken)
    {
        try
        {
            DriveFileItem createdFolder = await mDriveService.CreateFolderAsync(teamRootFolderID, teamDisplayName, cancellationToken);
            summary.CreatedCount = summary.CreatedCount + 1;
            DriveFileMetadata? createdMetadataOrNull = await mDriveService.GetFileOrNullAsync(createdFolder.ID, cancellationToken);
            if (createdMetadataOrNull != null)
            {
                return createdMetadataOrNull;
            }

            DriveFileMetadata createdMetadata = new DriveFileMetadata();
            createdMetadata.ID = createdFolder.ID;
            createdMetadata.Name = createdFolder.Name;
            createdMetadata.MimeType = GOOGLE_DRIVE_FOLDER_MIME_TYPE;
            createdMetadata.ParentFolderIDs = new List<string> { teamRootFolderID };
            return createdMetadata;
        }
        catch (Exception exception)
        {
            failureMessages.Add(teamDisplayName + " 폴더를 만들지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task<DriveFileMetadata?> getChildFolderByNameOrNullAsync(string parentFolderID, string folderName, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files = await mDriveService.ListFilesAsync(parentFolderID, cancellationToken);
        DriveFileItem? folderOrNull = files.FirstOrDefault(file => file.IsFolder && file.Name == folderName);
        if (folderOrNull == null)
        {
            return null;
        }

        return await mDriveService.GetFileOrNullAsync(folderOrNull.ID, cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameIfConfiguredAsync(string? folderIDOrNull, string standardName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderIDOrNull))
        {
            summary.SkippedCount = summary.SkippedCount + 1;
            return null;
        }

        string folderID = folderIDOrNull.Trim();
        return await applyFolderNameByIDAsync(folderID, standardName, summary, failureMessages, appliedFolderIDs, cancellationToken);
    }

    private async Task<DriveFileMetadata?> applyFolderNameByIDAsync(string folderID, string standardName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, HashSet<string> appliedFolderIDs, CancellationToken cancellationToken)
    {
        if (appliedFolderIDs.Contains(folderID))
        {
            return await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        }

        appliedFolderIDs.Add(folderID);
        summary.TargetCount = summary.TargetCount + 1;
        try
        {
            DriveFileMetadata? metadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
            if (metadataOrNull == null)
            {
                failureMessages.Add(standardName + " 폴더를 찾지 못했습니다.");
                return null;
            }

            if (metadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
            {
                failureMessages.Add(standardName + " 저장 대상이 폴더가 아닙니다.");
                return null;
            }

            if (metadataOrNull.Name == standardName)
            {
                summary.AlreadyStandardCount = summary.AlreadyStandardCount + 1;
                return metadataOrNull;
            }

            DriveFileMetadata renamedMetadata = await mDriveService.RenameDriveItemAsync(folderID, standardName, cancellationToken);
            summary.RenamedCount = summary.RenamedCount + 1;
            return renamedMetadata;
        }
        catch (Exception exception)
        {
            failureMessages.Add(standardName + " 폴더명을 변경하지 못했습니다. " + exception.Message);
            return null;
        }
    }

    private async Task moveFolderIfNeededAsync(DriveFileMetadata? folderMetadataOrNull, string? targetParentFolderIDOrNull, string folderDisplayName, GoogleDriveFolderNameSyncSummary summary, List<string> failureMessages, CancellationToken cancellationToken)
    {
        if (folderMetadataOrNull == null || string.IsNullOrWhiteSpace(targetParentFolderIDOrNull))
        {
            return;
        }

        string targetParentFolderID = targetParentFolderIDOrNull.Trim();
        if (folderMetadataOrNull.ParentFolderIDs.Contains(targetParentFolderID))
        {
            return;
        }

        try
        {
            await mDriveService.MoveDriveItemToFolderAsync(folderMetadataOrNull.ID, targetParentFolderID, cancellationToken);
            summary.MovedCount = summary.MovedCount + 1;
        }
        catch (Exception exception)
        {
            failureMessages.Add(folderDisplayName + " 폴더 위치를 정리하지 못했습니다. " + exception.Message);
        }
    }

    private IReadOnlySet<string> getSharedParentFolderIDs(DriveFileMetadata? firstMetadataOrNull, DriveFileMetadata? secondMetadataOrNull, string? officialRootFolderIDOrNull)
    {
        HashSet<string> sharedParentFolderIDs = new HashSet<string>();
        if (firstMetadataOrNull == null || secondMetadataOrNull == null)
        {
            return sharedParentFolderIDs;
        }

        HashSet<string> secondParentFolderIDs = new HashSet<string>(secondMetadataOrNull.ParentFolderIDs);
        foreach (string parentFolderID in firstMetadataOrNull.ParentFolderIDs)
        {
            if (secondParentFolderIDs.Contains(parentFolderID) && isOfficialRootFolderID(parentFolderID, officialRootFolderIDOrNull) == false)
            {
                sharedParentFolderIDs.Add(parentFolderID);
            }
        }

        return sharedParentFolderIDs;
    }

    private bool isOfficialRootFolderID(string folderID, string? officialRootFolderIDOrNull)
    {
        return string.IsNullOrWhiteSpace(officialRootFolderIDOrNull) == false && folderID == officialRootFolderIDOrNull.Trim();
    }

    private string createFolderNameFailureMessage(IReadOnlyList<string> failureMessages)
    {
        List<string> visibleMessages = failureMessages.Take(3).ToList();
        string message = "일부 Drive 폴더명을 변경하지 못했습니다. " + string.Join(" / ", visibleMessages);
        if (failureMessages.Count > visibleMessages.Count)
        {
            message = message + " 외 " + (failureMessages.Count - visibleMessages.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) + "건";
        }

        return message;
    }
}
