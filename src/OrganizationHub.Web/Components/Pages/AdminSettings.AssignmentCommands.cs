using System.Threading;
using System.Threading.Tasks;
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

        ServiceResult folderAssignmentResult = await GoogleWorkspaceSetupService.AssignFolderAsync(mSelectedFolderTargetKey, DriveFolderId.CreateOptional(folder.Id), CancellationToken.None);
        showOperationResult(folderAssignmentResult, folderAssignmentResult.IsSuccess ? folder.Name + " 폴더를 연결했습니다." : string.Empty);
        if (folderAssignmentResult.IsSuccess)
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

        if (mSelectedFileAssignmentTarget == ESettingFileAssignmentTarget.NoticeFile)
        {
            mFormModel.FileResources.NoticeFileId = file.Id;
        }
        else
        {
            mFormModel.FileResources.WorkGuidelineFileId = file.Id;
        }

        ServiceResult fileAssignmentResult = await TeamManagementService.UpdateGlobalSettingAsync(mFormModel, CancellationToken.None);
        showOperationResult(fileAssignmentResult, fileAssignmentResult.IsSuccess ? file.Name + " 파일을 연결했습니다." : string.Empty);
        if (fileAssignmentResult.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult calendarAssignmentResult = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, CalendarId.CreateOptional(calendar.Id), CancellationToken.None);
        showOperationResult(calendarAssignmentResult, calendarAssignmentResult.IsSuccess ? calendar.Summary + " Calendar를 연결했습니다." : string.Empty);
        if (calendarAssignmentResult.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult taskListAssignmentResult = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, TodoTaskListId.CreateOptional(taskList.Id), CancellationToken.None);
        showOperationResult(taskListAssignmentResult, taskListAssignmentResult.IsSuccess ? taskList.Title + " Google Tasks 목록을 연결했습니다." : string.Empty);
        if (taskListAssignmentResult.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private void setFolderTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedFolderTargetKey = targetKey;
    }

    private void setCalendarTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedCalendarTargetKey = targetKey;
    }

    private void setTaskListTarget(GoogleWorkspaceAssignmentTargetKey targetKey)
    {
        mSelectedTaskListTargetKey = targetKey;
    }

    private void setFileAssignmentTarget(ESettingFileAssignmentTarget assignmentTarget)
    {
        mSelectedFileAssignmentTarget = assignmentTarget;
    }
}
