using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private const int FIRST_PARENT_FOLDER_USAGE_COUNT = 1;
    private const int PARENT_FOLDER_USAGE_COUNT_INCREMENT = 1;

    private async Task applyTeamFolderNamesAsync(
        GlobalSetting setting,
        IReadOnlyList<Team> teams,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        List<GoogleWorkspaceTeamFolderNameContext> teamFolderNameContexts = new List<GoogleWorkspaceTeamFolderNameContext>();
        Dictionary<DriveFolderId, int> parentFolderUsageCounts = new Dictionary<DriveFolderId, int>();
        HashSet<DriveFolderId> teamRootCandidateFolderIds = new HashSet<DriveFolderId>();
        foreach (Team team in teams)
        {
            DriveFileMetadata? materialsMetadataOrNull = await applyFolderNameIfConfiguredAsync(
                team.MaterialsFolderId,
                OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME,
                syncContext,
                cancellationToken);
            DriveFileMetadata? approvedMetadataOrNull = await applyFolderNameIfConfiguredAsync(
                team.ApprovedFolderId,
                OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME,
                syncContext,
                cancellationToken);
            IReadOnlySet<string> teamParentFolderIds = getSharedParentFolderIds(
                materialsMetadataOrNull,
                approvedMetadataOrNull,
                setting.OfficialRootFolderId);
            if (teamParentFolderIds.Count != 1)
            {
                continue;
            }

            DriveFolderId teamParentFolderId = DriveFolderId.CreateOptional(teamParentFolderIds.First());
            teamFolderNameContexts.Add(new GoogleWorkspaceTeamFolderNameContext(team, teamParentFolderId, materialsMetadataOrNull, approvedMetadataOrNull));
            if (parentFolderUsageCounts.ContainsKey(teamParentFolderId))
            {
                parentFolderUsageCounts[teamParentFolderId] = parentFolderUsageCounts[teamParentFolderId] + PARENT_FOLDER_USAGE_COUNT_INCREMENT;
            }
            else
            {
                parentFolderUsageCounts[teamParentFolderId] = FIRST_PARENT_FOLDER_USAGE_COUNT;
            }
        }

        foreach (GoogleWorkspaceTeamFolderNameContext teamFolderNameContext in teamFolderNameContexts)
        {
            if (parentFolderUsageCounts[teamFolderNameContext.TeamParentFolderId] > FIRST_PARENT_FOLDER_USAGE_COUNT)
            {
                await applyNestedTeamFolderNamesAsync(teamFolderNameContext, teamRootCandidateFolderIds, syncContext, cancellationToken);
                continue;
            }

            await applySingleTeamFolderNameAsync(
                setting,
                teamFolderNameContext,
                teamRootCandidateFolderIds,
                syncContext,
                cancellationToken);
        }

        if (teamRootCandidateFolderIds.Count == 1)
        {
            DriveFolderId teamRootFolderId = teamRootCandidateFolderIds.First();
            await applyFolderNameByIdAsync(
                teamRootFolderId,
                OrganizationHubWorkspaceDefaults.TEAM_ROOT_FOLDER_NAME,
                syncContext,
                cancellationToken);
        }
    }

    private async Task applyNestedTeamFolderNamesAsync(
        GoogleWorkspaceTeamFolderNameContext teamFolderNameContext,
        HashSet<DriveFolderId> teamRootCandidateFolderIds,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        teamRootCandidateFolderIds.Add(teamFolderNameContext.TeamParentFolderId);
        DriveFileMetadata? nestedTeamParentMetadataOrNull = await getOrCreateTeamParentFolderAsync(
            teamFolderNameContext.TeamParentFolderId,
            teamFolderNameContext.Team.DisplayName,
            syncContext,
            cancellationToken);
        if (nestedTeamParentMetadataOrNull == null)
        {
            return;
        }

        await moveFolderIfNeededAsync(
            teamFolderNameContext.MaterialsMetadataOrNull,
            nestedTeamParentMetadataOrNull.Id,
            teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.TEAM_MATERIALS_FOLDER_NAME,
            syncContext,
            cancellationToken);
        await moveFolderIfNeededAsync(
            teamFolderNameContext.ApprovedMetadataOrNull,
            nestedTeamParentMetadataOrNull.Id,
            teamFolderNameContext.Team.DisplayName + " " + OrganizationHubWorkspaceDefaults.APPROVED_FOLDER_NAME,
            syncContext,
            cancellationToken);
    }

    private async Task applySingleTeamFolderNameAsync(
        GlobalSetting setting,
        GoogleWorkspaceTeamFolderNameContext teamFolderNameContext,
        HashSet<DriveFolderId> teamRootCandidateFolderIds,
        DriveFolderNameSyncContext syncContext,
        CancellationToken cancellationToken)
    {
        DriveFileMetadata? teamParentMetadataOrNull = await applyFolderNameByIdAsync(
            teamFolderNameContext.TeamParentFolderId,
            teamFolderNameContext.Team.DisplayName,
            syncContext,
            cancellationToken);
        if (teamParentMetadataOrNull == null)
        {
            return;
        }

        foreach (string parentFolderId in teamParentMetadataOrNull.ParentFolderIds)
        {
            if (isOfficialRootFolderId(parentFolderId, setting.OfficialRootFolderId) == false)
            {
                teamRootCandidateFolderIds.Add(DriveFolderId.CreateOptional(parentFolderId));
            }
        }
    }
}
