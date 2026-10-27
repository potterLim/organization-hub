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
            await reloadResourceConfigurationAsync();
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

        if (await tryLoadUserSessionAsync() == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        SettingFileAssignmentCommand command = new SettingFileAssignmentCommand
        {
            Target = mSelectedFileAssignmentTarget,
            FileId = DriveItemId.CreateRequired(file.Id),
            ExpectedRevision = mFormModel.FileResourcesRevision
        };
        ServiceResult fileAssignmentResult = await GlobalSettingService.AssignFileAsync(CurrentUserSessionOrNull, command, CancellationToken.None);
        showOperationResult(fileAssignmentResult, fileAssignmentResult.IsSuccess ? file.Name + " 파일을 연결했습니다." : string.Empty);
        if (fileAssignmentResult.IsSuccess)
        {
            await reloadResourceConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult calendarAssignmentResult = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, CalendarId.CreateOptional(calendar.Id), CancellationToken.None);
        showOperationResult(calendarAssignmentResult, calendarAssignmentResult.IsSuccess ? calendar.Summary + " Calendar를 연결했습니다." : string.Empty);
        if (calendarAssignmentResult.IsSuccess)
        {
            await reloadResourceConfigurationAsync();
            await loadAssignmentOptionsAsync();
        }
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult taskListAssignmentResult = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, TodoTaskListId.CreateOptional(taskList.Id), CancellationToken.None);
        showOperationResult(taskListAssignmentResult, taskListAssignmentResult.IsSuccess ? taskList.Title + " Google Tasks 목록을 연결했습니다." : string.Empty);
        if (taskListAssignmentResult.IsSuccess)
        {
            await reloadResourceConfigurationAsync();
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
