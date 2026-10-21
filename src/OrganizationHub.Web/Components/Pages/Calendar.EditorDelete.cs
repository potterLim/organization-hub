using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private string mDeletionConflictMessage = string.Empty;
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
        bool hasWritableCalendar = deleteCandidate.CanDelete && mDeletableCalendarIds.Contains(calendarId.GetRequiredValue());
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
            await CalendarService.DeleteEventAsync(currentUserSession, calendarId, CalendarEventId.CreateOptional(deleteCandidate.Id), deleteCandidate.ETag, CancellationToken.None);
        }
        catch (Services.ResourceEditConflictException)
        {
            if (auditLogId.HasValue)
            {
                await markDeletionFailedAsync(auditLogId, CalendarEventDeletionFailureMessage.CreateOptional("삭제 전에 일정이 변경되었습니다."));
            }
            await loadEventsAsync();
            mDeletionConflictMessage = "다른 사용자가 일정을 변경하여 삭제하지 않았습니다. 최신 내용을 확인한 뒤 다시 삭제해 주세요.";
            return;
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
            Logger.LogWarning(exception, "Calendar event {EventId} deletion failed.", deleteCandidate.Id);
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
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Calendar deletion audit {AuditLogId} could not be marked successful.", auditLogId);
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
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Calendar deletion audit {AuditLogId} could not be marked failed.", auditLogId);
        }
    }
}
