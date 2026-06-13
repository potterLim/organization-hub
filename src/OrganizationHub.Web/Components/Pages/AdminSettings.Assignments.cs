using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private async Task assignFolderAsync(DriveFileItem folder)
    {
        if (folder.IsFolder == false)
        {
            return;
        }

        ServiceResult result = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, folder.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? folder.Name + " 폴더를 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignFileAsync(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return;
        }

        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            mFormModel.NoticeFileID = file.ID;
        }
        else
        {
            mFormModel.WorkGuidelineFileID = file.ID;
        }

        ServiceResult result = await TeamManagementService.UpdateGlobalSettingAsync(mFormModel, CancellationToken.None);
        showResult(result, result.IsSuccess ? file.Name + " 파일을 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, calendar.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? calendar.Summary + " Calendar를 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult result = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, taskList.ID, CancellationToken.None);
        showResult(result, result.IsSuccess ? taskList.Title + " Google Tasks 목록을 연결했습니다." : string.Empty);
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private void setFolderTarget(string targetKey)
    {
        mSelectedFolderTargetKey = targetKey;
    }

    private void setCalendarTarget(string targetKey)
    {
        mSelectedCalendarTargetKey = targetKey;
    }

    private void setTaskListTarget(string targetKey)
    {
        mSelectedTaskListTargetKey = targetKey;
    }

    private void setFileTarget(string targetKey)
    {
        mSelectedFileTargetKey = targetKey;
    }

    private string getSelectedFolderAssignmentDisplayName()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedFolderAssignmentDetail()
    {
        GoogleFolderAssignmentOption? optionOrNull = getSelectedFolderAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getDriveResourceName(getFolderAssignmentID(optionOrNull));
    }

    private string getSelectedFileAssignmentDisplayName()
    {
        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            return "공지 파일";
        }

        return "업무 기준 파일";
    }

    private string getSelectedFileAssignmentDetail()
    {
        if (mSelectedFileTargetKey == NOTICE_FILE_TARGET_KEY)
        {
            return getDriveResourceName(mFormModel.NoticeFileID);
        }

        return getDriveResourceName(mFormModel.WorkGuidelineFileID);
    }

    private string getSelectedCalendarAssignmentDisplayName()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedCalendarAssignmentDetail()
    {
        GoogleCalendarAssignmentOption? optionOrNull = getSelectedCalendarAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getCalendarName(getCalendarAssignmentID(optionOrNull));
    }

    private string getSelectedTaskListAssignmentDisplayName()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "대상을 선택해 주세요";
        }

        return optionOrNull.DisplayName;
    }

    private string getSelectedTaskListAssignmentDetail()
    {
        GoogleTaskListAssignmentOption? optionOrNull = getSelectedTaskListAssignmentOptionOrNull();
        if (optionOrNull == null)
        {
            return "저장 대상이 없습니다.";
        }

        return getTaskListName(getTaskListAssignmentID(optionOrNull));
    }

    private GoogleFolderAssignmentOption? getSelectedFolderAssignmentOptionOrNull()
    {
        return mFolderAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedFolderTargetKey);
    }

    private GoogleCalendarAssignmentOption? getSelectedCalendarAssignmentOptionOrNull()
    {
        return mCalendarAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedCalendarTargetKey);
    }

    private GoogleTaskListAssignmentOption? getSelectedTaskListAssignmentOptionOrNull()
    {
        return mTaskListAssignmentOptions.FirstOrDefault(option => option.TargetKey == mSelectedTaskListTargetKey);
    }

    private string getFolderAssignmentID(GoogleFolderAssignmentOption option)
    {
        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.OfficialRoot)
        {
            return mFormModel.OfficialRootFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Public)
        {
            return mFormModel.PublicFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.Common)
        {
            return mFormModel.CommonFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.ResponsibleResources)
        {
            return mFormModel.ResponsibleResourcesFolderID;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.WorkResources)
        {
            return mFormModel.WorkResourcesFolderID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        if (option.FolderAssignmentKind == EGoogleFolderAssignmentKind.TeamMaterials)
        {
            return getNullAsEmpty(teamOrNull.MaterialsFolderID);
        }

        return getNullAsEmpty(teamOrNull.ApprovedFolderID);
    }

    private string getCalendarAssignmentID(GoogleCalendarAssignmentOption option)
    {
        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Organization)
        {
            return mFormModel.OrganizationCalendarID;
        }

        if (option.CalendarAssignmentKind == EGoogleCalendarAssignmentKind.Leader)
        {
            return mFormModel.ResponsibleCalendarID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getNullAsEmpty(teamOrNull.CalendarID);
    }

    private string getTaskListAssignmentID(GoogleTaskListAssignmentOption option)
    {
        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Organization)
        {
            return mFormModel.OrganizationTaskListID;
        }

        if (option.TaskListAssignmentKind == EGoogleTaskListAssignmentKind.Leader)
        {
            return mFormModel.ResponsibleTaskListID;
        }

        Team? teamOrNull = getTeamOrNull(option.TeamID);
        if (teamOrNull == null)
        {
            return string.Empty;
        }

        return getNullAsEmpty(teamOrNull.TaskListID);
    }

    private Team? getTeamOrNull(int? teamIDOrNull)
    {
        if (teamIDOrNull.HasValue == false)
        {
            return null;
        }

        return mTeams.FirstOrDefault(team => team.ID == teamIDOrNull.Value);
    }
}
