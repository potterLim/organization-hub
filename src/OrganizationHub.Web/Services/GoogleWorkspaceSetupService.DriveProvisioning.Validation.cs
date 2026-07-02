using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleWorkspaceSetupService
{
    private void validateStandardDriveAssignments(
        GlobalSetting setting,
        IReadOnlyList<Team> teams,
        List<string> failureMessages
    )
    {
        addMissingDriveAssignmentFailure(setting.OfficialRootFolderId, "기본 자료 위치", failureMessages);
        addMissingDriveAssignmentFailure(setting.NoticeFileId, "공지 파일", failureMessages);
        addMissingDriveAssignmentFailure(setting.WorkGuidelineFileId, "업무 기준 파일", failureMessages);
        addMissingDriveAssignmentFailure(setting.PublicFolderId, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, failureMessages);
        addMissingDriveAssignmentFailure(setting.CommonFolderId, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, failureMessages);
        addMissingDriveAssignmentFailure(setting.ResponsibleResourcesFolderId, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, failureMessages);
        addMissingDriveAssignmentFailure(setting.WorkResourcesFolderId, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, failureMessages);

        foreach (Team team in teams)
        {
            if (new OrganizationTeamKey(team.TeamKey).IsSystemAdministrationTeamKey)
            {
                continue;
            }

            addMissingDriveAssignmentFailure(team.MaterialsFolderId, team.DisplayName + " 자료", failureMessages);
            addMissingDriveAssignmentFailure(team.ApprovedFolderId, team.DisplayName + " 결재 완료 자료", failureMessages);
        }
    }

    private void addMissingDriveAssignmentFailure(
        string? assignmentIdOrNull,
        string assignmentName,
        List<string> failureMessages
    )
    {
        if (string.IsNullOrWhiteSpace(assignmentIdOrNull) == false)
        {
            return;
        }

        failureMessages.Add(assignmentName + " 연결이 완료되지 않았습니다.");
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
