using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminSettings
{
    private async Task loadConfigurationAsync()
    {
        mFormModel = await TeamManagementService.GetGlobalSettingAsync(CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
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
        if (string.IsNullOrWhiteSpace(mSelectedFolderTargetKey) && mFolderAssignmentOptions.Count > 0)
        {
            mSelectedFolderTargetKey = mFolderAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleCalendarAssignmentOption> calendarOptions = await GoogleWorkspaceSetupService.GetCalendarAssignmentOptionsAsync(CancellationToken.None);
        mCalendarAssignmentOptions = calendarOptions.ToList();
        if (string.IsNullOrWhiteSpace(mSelectedCalendarTargetKey) && mCalendarAssignmentOptions.Count > 0)
        {
            mSelectedCalendarTargetKey = mCalendarAssignmentOptions[0].TargetKey;
        }

        IReadOnlyList<GoogleTaskListAssignmentOption> taskListOptions = await GoogleWorkspaceSetupService.GetTaskListAssignmentOptionsAsync(CancellationToken.None);
        mTaskListAssignmentOptions = taskListOptions.ToList();
        if (string.IsNullOrWhiteSpace(mSelectedTaskListTargetKey) && mTaskListAssignmentOptions.Count > 0)
        {
            mSelectedTaskListTargetKey = mTaskListAssignmentOptions[0].TargetKey;
        }
    }

    private async Task refreshAllAsync()
    {
        mIsRefreshing = true;
        try
        {
            await loadConfigurationAsync();
            await loadConnectionStatusAsync();
            await loadAssignmentOptionsAsync();
            if (mStatus.IsConnected)
            {
                Task driveItemsTask = loadDriveItemsAsync();
                Task calendarsTask = loadCalendarsAsync();
                Task taskListsTask = loadTaskListsAsync();
                Task driveResourceNamesTask = loadDriveResourceNamesAsync();
                await Task.WhenAll(driveItemsTask, calendarsTask, taskListsTask, driveResourceNamesTask);
            }

            mMessage = "설정을 다시 불러왔습니다.";
            mMessageSeverity = Severity.Success;
        }
        finally
        {
            mIsRefreshing = false;
        }
    }

    private async Task loadDriveItemsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        mIsLoadingDriveItems = true;
        try
        {
            IReadOnlyList<DriveFileItem> driveItems = await DriveService.ListFilesAsync(getCurrentFolderID(), CancellationToken.None);
            mDriveItems = driveItems.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Drive 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsLoadingDriveItems = false;
        }
    }

    private async Task loadCalendarsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        try
        {
            IReadOnlyList<GoogleCalendarListItem> calendars = await GoogleWorkspaceBrowserService.ListCalendarsAsync(CancellationToken.None);
            mCalendars = calendars.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Calendar 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task loadTaskListsAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        try
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = await GoogleWorkspaceBrowserService.ListTaskListsAsync(CancellationToken.None);
            mTaskLists = taskLists.ToList();
        }
        catch (Exception exception)
        {
            mMessage = "Google Tasks 목록을 불러오지 못했습니다. " + exception.Message;
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task loadDriveResourceNamesAsync()
    {
        if (mStatus.IsConnected == false)
        {
            return;
        }

        Dictionary<string, string> namesByID = new Dictionary<string, string>();
        foreach (string driveResourceID in getConfiguredDriveResourceIDs())
        {
            if (namesByID.ContainsKey(driveResourceID))
            {
                continue;
            }

            try
            {
                DriveFileMetadata? metadataOrNull = await DriveService.GetFileOrNullAsync(driveResourceID, CancellationToken.None);
                if (metadataOrNull == null)
                {
                    namesByID[driveResourceID] = "연결된 항목을 찾을 수 없음";
                    continue;
                }

                namesByID[driveResourceID] = metadataOrNull.Name;
            }
            catch
            {
                namesByID[driveResourceID] = "이름을 확인하지 못함";
            }
        }

        mDriveResourceNameByID = namesByID;
    }

    private async Task saveSettingAsync()
    {
        ServiceResult result = await TeamManagementService.UpdateGlobalSettingAsync(mFormModel, CancellationToken.None);
        showResult(result, "시스템 설정이 저장되었습니다.");
        if (result.IsSuccess)
        {
            await loadConfigurationAsync();
            await loadDriveResourceNamesAsync();
        }
    }

    private void showResult(ServiceResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = result.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }
}
