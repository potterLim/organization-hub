using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private void requestDelete(CalendarEventItem calendarEvent)
    {
        mDeleteCandidateOrNull = calendarEvent;
    }

    private void cancelDelete()
    {
        mDeleteCandidateOrNull = null;
    }

    private async Task deleteSelectedEventAsync()
    {
        CalendarEventItem? deleteCandidateOrNull = mDeleteCandidateOrNull;
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null || deleteCandidateOrNull == null)
        {
            return;
        }

        CalendarEventItem deleteCandidate = deleteCandidateOrNull;
        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarId calendarId = CalendarId.CreateOptional(deleteCandidate.CalendarId);
        bool hasWritableCalendar = mWritableCalendars.Any(calendar => calendar.CalendarId == calendarId);
        if (hasWritableCalendar == false)
        {
            mErrorMessage = "이 일정을 삭제할 권한이 없습니다.";
            mDeleteCandidateOrNull = null;
            return;
        }

        CalendarEventDeletionAuditLogId auditLogId = CalendarEventDeletionAuditLogId.Empty;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        mDeleteCandidateOrNull = null;
        mSelectedEventOrNull = null;
        mErrorMessage = string.Empty;
        removeEventFromLocalCollections(CalendarId.CreateOptional(deleteCandidate.CalendarId), CalendarEventId.CreateOptional(deleteCandidate.Id));
        await InvokeAsync(StateHasChanged);
        try
        {
            auditLogId = await CalendarEventDeletionAuditLogService.RecordPendingDeletionAsync(currentUserSession, deleteCandidate, CancellationToken.None);
            await CalendarService.DeleteEventAsync(currentUserSession, calendarId, CalendarEventId.CreateOptional(deleteCandidate.Id), CancellationToken.None);
        }
        catch (UnauthorizedAccessException)
        {
            if (auditLogId.HasValue)
            {
                await markDeletionFailedAsync(auditLogId, CalendarEventDeletionFailureMessage.CreateOptional("일정 삭제 권한이 변경되었습니다."));
            }

            mErrorMessage = "이 일정을 삭제할 권한이 없습니다.";
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            return;
        }
        catch (Exception exception)
        {
            if (auditLogId.HasValue)
            {
                await markDeletionFailedAsync(auditLogId, CalendarEventDeletionFailureMessage.CreateOptional(exception.Message));
            }

            mErrorMessage = "일정을 삭제하지 못했습니다. 일정은 아직 남아 있습니다. 잠시 후 다시 시도해 주세요.";
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            return;
        }

        string completionWarning = string.Empty;
        try
        {
            await CalendarEventDeletionAuditLogService.MarkDeletionSucceededAsync(auditLogId, CancellationToken.None);
            recordLoadedChangeSnapshot();
        }
        catch
        {
            completionWarning = "일정은 삭제됐지만 삭제 기록 상태를 갱신하지 못했습니다. 잠시 후 새로고침해 주세요.";
        }

        if (string.IsNullOrWhiteSpace(completionWarning) == false)
        {
            mErrorMessage = completionWarning;
        }
    }

    private async Task markDeletionFailedAsync(CalendarEventDeletionAuditLogId auditLogId, CalendarEventDeletionFailureMessage failureMessage)
    {
        try
        {
            await CalendarEventDeletionAuditLogService.MarkDeletionFailedAsync(auditLogId, failureMessage, CancellationToken.None);
        }
        catch
        {
        }
    }
}
