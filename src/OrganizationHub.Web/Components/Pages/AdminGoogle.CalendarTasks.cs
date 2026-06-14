using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MudBlazor;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminGoogle
{
    private async Task loadCalendarsAsync()
    {
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
        try
        {
            IReadOnlyList<GoogleTaskListItem> taskLists = await GoogleWorkspaceBrowserService.ListTaskListsAsync(CancellationToken.None);
            mTaskLists = taskLists.ToList();
        }
        catch (Exception exception)
        {
            mMessage = createGoogleTasksErrorMessage(exception);
            mMessageSeverity = Severity.Error;
        }
    }

    private async Task assignCalendarAsync(GoogleCalendarListItem calendar)
    {
        ServiceResult calendarAssignmentResult = await GoogleWorkspaceSetupService.AssignCalendarAsync(mSelectedCalendarTargetKey, CalendarId.CreateOptional(calendar.Id), CancellationToken.None);
        showOperationResult(calendarAssignmentResult, calendarAssignmentResult.IsSuccess ? calendar.Summary + " Calendar를 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task assignTaskListAsync(GoogleTaskListItem taskList)
    {
        ServiceResult taskListAssignmentResult = await GoogleWorkspaceSetupService.AssignTaskListAsync(mSelectedTaskListTargetKey, TodoTaskListId.CreateOptional(taskList.Id), CancellationToken.None);
        showOperationResult(taskListAssignmentResult, taskListAssignmentResult.IsSuccess ? taskList.Title + " Google Tasks 목록을 저장했습니다." : string.Empty);
        await loadHealthCheckAsync();
    }

    private async Task ensureStandardCalendarsAsync()
    {
        mIsEnsuringStandardCalendars = true;
        try
        {
            ServiceResultOfT<GoogleWorkspaceResourceSetupSummary> calendarSetupResult = await GoogleWorkspaceSetupService.EnsureStandardCalendarsAsync(CancellationToken.None);
            if (calendarSetupResult.IsSuccess && calendarSetupResult.ValueOrNull != null)
            {
                GoogleWorkspaceResourceSetupSummary summary = calendarSetupResult.ValueOrNull;
                mMessage = createSetupSummaryMessage("기본 Calendar 준비 완료", summary);
                mMessageSeverity = Severity.Success;
                await loadAssignmentOptionsAsync();
                await loadCalendarsAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = calendarSetupResult.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardCalendars = false;
        }
    }

    private async Task ensureStandardTaskListsAsync()
    {
        mIsEnsuringStandardTaskLists = true;
        try
        {
            ServiceResultOfT<GoogleTaskListSetupSummary> taskListSetupResult = await GoogleWorkspaceSetupService.EnsureStandardTaskListsAsync(CancellationToken.None);
            if (taskListSetupResult.IsSuccess && taskListSetupResult.ValueOrNull != null)
            {
                GoogleTaskListSetupSummary summary = taskListSetupResult.ValueOrNull;
                mMessage = "기본 Google Tasks 목록 준비 완료: 생성 " + summary.CreatedCount.ToString(CultureInfo.InvariantCulture) + "개, 재사용 " + summary.ReusedCount.ToString(CultureInfo.InvariantCulture) + "개, 매핑 " + summary.AssignedCount.ToString(CultureInfo.InvariantCulture) + "개";
                mMessageSeverity = Severity.Success;
                await loadTaskListsAsync();
                await loadHealthCheckAsync();
                return;
            }

            mMessage = taskListSetupResult.ErrorMessage;
            mMessageSeverity = Severity.Error;
        }
        finally
        {
            mIsEnsuringStandardTaskLists = false;
        }
    }

    private string createGoogleTasksErrorMessage(Exception exception)
    {
        return GoogleApiExceptionMessageFormatter.CreateGoogleTasksMessage(exception, "Google Tasks 목록을 불러오지 못했습니다.");
    }

    private string getCalendarAccessDisplayName(GoogleCalendarListItem calendar)
    {
        if (calendar.IsPrimary)
        {
            return "기본 Calendar";
        }

        switch (calendar.AccessRole)
        {
            case "owner":
                return "소유자";
            case "writer":
                return "편집 가능";
            case "reader":
                return "읽기 가능";
            default:
                return "접근 가능";
        }
    }
}
