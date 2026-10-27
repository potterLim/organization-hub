using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private async Task loadConfigurationAsync()
    {
        GlobalSettingFormModel form = await GlobalSettingService.GetGlobalSettingAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        mFormModel = form;
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
    }

    private async Task loadConnectionStatusAsync()
    {
        mStatus = await GoogleConnectionService.GetStatusAsync(CancellationToken.None);
    }

    private async Task loadAssignmentOptionsAsync()
    {
        IReadOnlyList<GoogleFolderAssignmentOption> folderOptions = await GoogleWorkspaceSetupService.GetFolderAssignmentOptionsAsync(CancellationToken.None);
        mFolderAssignmentOptions = folderOptions.ToList();
        if (mSelectedFolderTargetKey.HasValue == false && mFolderAssignmentOptions.Count > 0)
        {
            mSelectedFolderTargetKey = mFolderAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> calendarOptions = await GoogleWorkspaceSetupService.GetCalendarAssignmentOptionsAsync(CancellationToken.None);
        mCalendarAssignmentOptions = calendarOptions.ToList();
        if (mSelectedCalendarTargetKey.HasValue == false && mCalendarAssignmentOptions.Count > 0)
        {
            mSelectedCalendarTargetKey = mCalendarAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> taskListOptions = await GoogleWorkspaceSetupService.GetTaskListAssignmentOptionsAsync(CancellationToken.None);
        mTaskListAssignmentOptions = taskListOptions.ToList();
        if (mSelectedTaskListTargetKey.HasValue == false && mTaskListAssignmentOptions.Count > 0)
        {
            mSelectedTaskListTargetKey = mTaskListAssignmentOptions[0].TargetKey;
        }
    }

    private async Task refreshAllAsync()
    {
        mIsRefreshing = true;
        try
        {
            GlobalSettingFormModel displayDraft = mFormModel;
            await loadConfigurationAsync();
            mFormModel.ShouldShowTodoAuthor = displayDraft.ShouldShowTodoAuthor;
            mFormModel.ShouldShowCalendarAuthor = displayDraft.ShouldShowCalendarAuthor;
            mFormModel.DisplayOptionsRevision = displayDraft.DisplayOptionsRevision;
            await loadConnectionStatusAsync();
            await loadAssignmentOptionsAsync();
            if (mStatus.IsConnected)
            {
                ServiceResult[] results = await Task.WhenAll(loadDriveItemsAsync(), loadCalendarsAsync(), loadTaskListsAsync(), loadDriveResourceNamesAsync());
                if (results.Any(result => result.IsSuccess == false))
                {
                    mMessage = "일부 목록을 불러오지 못했습니다. 다시 시도해 주세요.";
                    mMessageSeverity = Severity.Warning;
                    return;
                }
            }

            mMessage = "연결된 자료 목록을 새로고침했습니다.";
            mMessageSeverity = Severity.Success;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Resource configuration refresh failed.");
            mMessage = "설정 정보를 불러오지 못했습니다. 다시 시도해 주세요.";
            mMessageSeverity = Severity.Warning;
        }
        finally
        {
            mIsRefreshing = false;
        }
    }

    private async Task<ServiceResult> loadDriveItemsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return ServiceResult.Succeed();
        }

        mIsLoadingDriveItems = true;
        try
        {
            IReadOnlyList<DriveFileItem> driveItems = await DriveService.ListFilesAsync(DriveFolderId.CreateOptional(getCurrentFolderId()), CancellationToken.None);
            mDriveItems = driveItems.ToList();
            return ServiceResult.Succeed();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Could not load Google configuration or connection status.");
            mMessage = "폴더 목록을 불러오지 못했습니다. 다시 불러와 주세요.";
            mMessageSeverity = Severity.Error;
            return ServiceResult.Fail(mMessage);
        }
        finally
        {
            mIsLoadingDriveItems = false;
        }
    }

    private async Task<ServiceResult> loadCalendarsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return ServiceResult.Succeed();
        }

        try
        {
            IReadOnlyList<GoogleCalendarListItem> calendars = await GoogleWorkspaceBrowserService.ListCalendarsAsync(CancellationToken.None);
            mCalendars = calendars.ToList();
            return ServiceResult.Succeed();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Could not load Google configuration or connection status.");
            mMessage = "캘린더 목록을 불러오지 못했습니다. 다시 불러와 주세요.";
            mMessageSeverity = Severity.Error;
            return ServiceResult.Fail(mMessage);
        }
    }

    private async Task<ServiceResult> loadTaskListsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return ServiceResult.Succeed();
        }

        try
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = await GoogleWorkspaceBrowserService.ListTaskListsAsync(CancellationToken.None);
            mTaskLists = taskLists.ToList();
            return ServiceResult.Succeed();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Could not load Google configuration or connection status.");
            mMessage = "할 일 목록을 불러오지 못했습니다. 다시 불러와 주세요.";
            mMessageSeverity = Severity.Error;
            return ServiceResult.Fail(mMessage);
        }
    }

    private async Task<ServiceResult> loadDriveResourceNamesAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return ServiceResult.Succeed();
        }

        Dictionary<string, string> namesById = new Dictionary<string, string>();
        bool hasFailure = false;
        foreach (string driveResourceId in getConfiguredDriveResourceIds())
        {
            if (namesById.ContainsKey(driveResourceId))
            {
                continue;
            }

            try
            {
                DriveFileMetadata? metadataOrNull = await DriveService.GetFileOrNullAsync(DriveItemId.CreateOptional(driveResourceId), CancellationToken.None);
                if (metadataOrNull == null)
                {
                    namesById[driveResourceId] = "연결된 항목을 찾을 수 없음";
                    continue;
                }

                namesById[driveResourceId] = metadataOrNull.Name;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Configured Drive resource name could not be loaded.");
                hasFailure = true;
                namesById[driveResourceId] = "이름을 확인하지 못함";
            }
        }

        mDriveResourceNameById = namesById;
        return hasFailure ? ServiceResult.Fail("일부 자료 이름을 확인하지 못했습니다.") : ServiceResult.Succeed();
    }

    private async Task saveSettingAsync()
    {
        if (await tryLoadUserSessionAsync() == false || CurrentUserSessionOrNull == null)
        {
            return;
        }

        ServiceResult saveResult = await GlobalSettingService.UpdateDisplayOptionsAsync(CurrentUserSessionOrNull, mFormModel, CancellationToken.None);
        showOperationResult(saveResult, "시스템 설정이 저장되었습니다.");
        if (saveResult.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
