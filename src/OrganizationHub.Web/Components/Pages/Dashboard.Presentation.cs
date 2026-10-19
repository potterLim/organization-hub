using System;
using System.Collections.Generic;
using System.Globalization;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components.Pages;

public partial class Dashboard
{
    private string formatEventTime(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.Start.HasValue == false)
        {
            return "시간 미정";
        }

        if (calendarEvent.IsAllDay)
        {
            return getClientEventStart(calendarEvent).ToString("MM-dd", CultureInfo.InvariantCulture) + " 종일";
        }

        string startText = getClientEventStart(calendarEvent).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (calendarEvent.End.HasValue == false)
        {
            return startText;
        }

        DateTime localEnd = ClientTimeZoneUtility.createClientDateTime(calendarEvent.End.Value.UtcDateTime, mClientTimeZoneInfo);
        return startText + " - " + localEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private DateTime getClientEventStart(CalendarEventItem calendarEvent)
    {
        DateTimeOffset eventStart = calendarEvent.Start.GetValueOrDefault();
        return ClientTimeZoneUtility.createClientDateTime(eventStart.UtcDateTime, mClientTimeZoneInfo);
    }

    private string formatDashboardEventMeta(CalendarEventItem calendarEvent)
    {
        List<string> metadataParts = new List<string>();
        if (string.IsNullOrWhiteSpace(calendarEvent.CalendarName) == false)
        {
            metadataParts.Add(calendarEvent.CalendarName);
        }

        if (shouldShowCalendarAuthor(calendarEvent))
        {
            metadataParts.Add("작성자: " + calendarEvent.CreatedByDisplayName);
        }

        return string.Join(" · ", metadataParts);
    }

    private string formatDashboardEventLocation(CalendarEventItem calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            return string.Empty;
        }

        return "장소: " + calendarEvent.Location.Trim();
    }

    private string formatDashboardTodoMeta(TodoTaskItem task)
    {
        List<string> metadataParts = new List<string>();
        if (string.IsNullOrWhiteSpace(task.TaskListDisplayName) == false)
        {
            metadataParts.Add(task.TaskListDisplayName);
        }

        if (mShouldShowTodoAuthor && string.IsNullOrWhiteSpace(task.CreatedByDisplayName) == false)
        {
            metadataParts.Add("작성자: " + task.CreatedByDisplayName);
        }

        return string.Join(" · ", metadataParts);
    }

    private string getDashboardTodoDateClass(TodoTaskItem task)
    {
        string className = "hub-list-side hub-dashboard-row-side hub-dashboard-todo-date hub-due-status";
        if (isDashboardTodoOverdue(task))
        {
            return className + " hub-due-status-overdue";
        }

        if (isDashboardTodoDueToday(task))
        {
            return className + " hub-due-status-today";
        }

        return className;
    }

    private string formatDashboardTodoDate(TodoTaskItem task)
    {
        DateTime? dueAtOrNull = getTaskDueDisplayDateOrNull(task);
        if (dueAtOrNull.HasValue == false || task.DueAt.HasValue == false)
        {
            return string.Empty;
        }

        string dueText = dueAtOrNull.Value.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        DateTime clientNow = ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo);
        DateTime dueAtUtc = ClientTimeZoneUtility.normalizeUtcDateTimeValue(task.DueAt.Value);
        if (dueAtUtc < DateTime.UtcNow)
        {
            return "기한 지남: " + dueText;
        }

        if (dueAtOrNull.Value.Date == clientNow.Date)
        {
            return "오늘 마감: " + dueAtOrNull.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return "마감: " + dueText;
    }

    private bool isDashboardTodoOverdue(TodoTaskItem task)
    {
        if (task.DueAt.HasValue == false)
        {
            return false;
        }

        return ClientTimeZoneUtility.normalizeUtcDateTimeValue(task.DueAt.Value) < DateTime.UtcNow;
    }

    private bool isDashboardTodoDueToday(TodoTaskItem task)
    {
        DateTime? dueAtOrNull = getTaskDueDisplayDateOrNull(task);
        DateTime clientNow = ClientTimeZoneUtility.getClientNow(mClientTimeZoneInfo);
        return dueAtOrNull.HasValue && dueAtOrNull.Value.Date == clientNow.Date;
    }

    private bool shouldShowCalendarAuthor(CalendarEventItem calendarEvent)
    {
        return mShouldShowCalendarAuthor && string.IsNullOrWhiteSpace(calendarEvent.CreatedByDisplayName) == false;
    }

    private string formatDashboardApprovalRequesterMeta(ApprovalRequestItem approvalRequest)
    {
        string requesterText = approvalRequest.GetRequesterAffiliationText();
        if (string.IsNullOrWhiteSpace(requesterText))
        {
            return "상신자 정보 없음";
        }

        return "상신자: " + requesterText;
    }

    private string getCurrentApprovalStepLabel(ApprovalRequestItem approvalRequest)
    {
        if (approvalRequest.Status == EApprovalRequestStatus.Publishing)
        {
            return "승인본 저장 중";
        }

        ApprovalStepCount stepCount = new ApprovalStepCount(approvalRequest.StepCount);
        return ApprovalStepLabelUtility.GetStepLabel(stepCount, new ApprovalStepNumber(approvalRequest.CurrentStepNumber));
    }
}
