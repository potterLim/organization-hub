using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private void openCreateEditor()
    {
        DateTime nextStart = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
        if (nextStart <= DateTime.Now)
        {
            nextStart = nextStart.AddHours(1);
        }

        mIsEditorOpen = true;
        mIsEditing = false;
        mSelectedEventOrNull = null;
        mSelectedDayOrNull = null;
        mEditorEventID = string.Empty;
        mEditorSummary = string.Empty;
        mEditorDescription = string.Empty;
        mEditorLocation = string.Empty;
        mEditorStartText = formatEditorDateTime(nextStart);
        mEditorEndText = formatEditorDateTime(nextStart.AddHours(1));
        mEditorIsAllDay = false;
        mEditorMessage = string.Empty;
        mEditorCalendarID = mWritableCalendars.Count > 0 ? mWritableCalendars[0].CalendarID : string.Empty;
    }

    private void openEditEditor(CalendarEventItem calendarEvent)
    {
        mIsEditorOpen = true;
        mIsEditing = true;
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mEditorEventID = calendarEvent.ID;
        mEditorCalendarID = calendarEvent.CalendarID;
        mEditorSummary = calendarEvent.Summary;
        mEditorDescription = calendarEvent.Description;
        mEditorLocation = calendarEvent.Location;
        mEditorIsAllDay = calendarEvent.IsAllDay;
        mEditorStartText = calendarEvent.Start.HasValue ? formatEditorDateTime(calendarEvent.Start.Value.ToLocalTime().DateTime) : formatEditorDateTime(DateTime.Now);
        mEditorEndText = calendarEvent.End.HasValue ? formatEditorDateTime(calendarEvent.End.Value.ToLocalTime().DateTime) : formatEditorDateTime(DateTime.Now.AddHours(1));
        mEditorMessage = string.Empty;
    }

    private void closeEditor()
    {
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void selectEvent(CalendarEventItem calendarEvent)
    {
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private void clearSelectedEvent()
    {
        mSelectedEventOrNull = null;
        mIsEditorOpen = false;
        mEditorMessage = string.Empty;
    }

    private Task handleEditorCalendarChangedAsync(string calendarID)
    {
        mEditorCalendarID = calendarID;
        return Task.CompletedTask;
    }

    private async Task saveEventAsync()
    {
        UserSession? currentUserSessionOrNull = CurrentUserSessionOrNull;
        if (currentUserSessionOrNull == null)
        {
            return;
        }

        UserSession currentUserSession = currentUserSessionOrNull;
        CalendarReference? calendarOrNull = mWritableCalendars.FirstOrDefault(calendar => calendar.CalendarID == mEditorCalendarID);
        if (calendarOrNull == null)
        {
            mEditorMessage = "저장할 일정 위치를 선택해 주세요.";
            return;
        }

        CalendarEventFormModel? formModelOrNull = createEventFormOrNull();
        if (formModelOrNull == null)
        {
            return;
        }

        bool canWrite = await PermissionService.CanWriteCalendarAsync(currentUserSession, formModelOrNull.CalendarID, CancellationToken.None);
        if (canWrite == false)
        {
            mEditorMessage = "이 일정 위치를 수정할 권한이 없습니다.";
            return;
        }

        bool isEditing = mIsEditing;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        try
        {
            CalendarEventItem optimisticEvent = createOptimisticCalendarEvent(formModelOrNull, calendarOrNull.DisplayName, currentUserSession);
            applyWritableState(optimisticEvent);
            mErrorMessage = string.Empty;
            closeEditor();
            applySavedEvent(optimisticEvent);
            await InvokeAsync(StateHasChanged);
            CalendarEventItem savedEvent;
            if (isEditing)
            {
                savedEvent = await CalendarService.UpdateEventAsync(formModelOrNull, calendarOrNull.DisplayName, CancellationToken.None);
            }
            else
            {
                savedEvent = await CalendarService.CreateEventAsync(currentUserSession, formModelOrNull, calendarOrNull.DisplayName, CancellationToken.None);
            }

            applyWritableState(savedEvent);
            if (isEditing == false)
            {
                removeEventFromMonthList(optimisticEvent.CalendarID, optimisticEvent.ID);
            }

            applySavedEvent(savedEvent);
            recordLoadedChangeSnapshot();
        }
        catch
        {
            mMonthEvents = cloneCalendarEvents(previousMonthEvents);
            refreshDerivedCalendarCollections();
            mSelectedEventOrNull = previousSelectedEventOrNull;
            mErrorMessage = "일정을 저장하지 못했습니다. 변경 내용은 저장되지 않았습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    private CalendarEventFormModel? createEventFormOrNull()
    {
        if (string.IsNullOrWhiteSpace(mEditorSummary))
        {
            mEditorMessage = "일정 제목을 입력해 주세요.";
            return null;
        }

        DateTime startLocal;
        DateTime endLocal;
        bool isStartParsed = DateTime.TryParse(mEditorStartText, CultureInfo.CurrentCulture, DateTimeStyles.None, out startLocal);
        bool isEndParsed = DateTime.TryParse(mEditorEndText, CultureInfo.CurrentCulture, DateTimeStyles.None, out endLocal);
        if (isStartParsed == false || isEndParsed == false)
        {
            mEditorMessage = "시작과 종료 시간을 입력해 주세요. 예: 2026-05-22 10:00";
            return null;
        }

        if (mEditorIsAllDay && endLocal.Date < startLocal.Date)
        {
            mEditorMessage = "종일 일정의 종료일은 시작일보다 빠를 수 없습니다.";
            return null;
        }

        if (mEditorIsAllDay == false && endLocal <= startLocal)
        {
            mEditorMessage = "종료 시간은 시작 시간보다 늦어야 합니다.";
            return null;
        }

        CalendarEventFormModel formModel = new CalendarEventFormModel();
        formModel.ID = mEditorEventID;
        formModel.CalendarID = mEditorCalendarID;
        formModel.Summary = mEditorSummary;
        formModel.Description = mEditorDescription;
        formModel.Location = mEditorLocation;
        formModel.StartLocal = startLocal;
        formModel.EndLocal = endLocal;
        formModel.IsAllDay = mEditorIsAllDay;
        return formModel;
    }

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
        bool canWrite = await PermissionService.CanWriteCalendarAsync(currentUserSession, deleteCandidate.CalendarID, CancellationToken.None);
        if (canWrite == false)
        {
            mErrorMessage = "이 일정을 삭제할 권한이 없습니다.";
            mDeleteCandidateOrNull = null;
            return;
        }

        int auditLogID = 0;
        List<CalendarEventItem> previousMonthEvents = cloneCalendarEvents(mMonthEvents);
        CalendarEventItem? previousSelectedEventOrNull = cloneCalendarEventOrNull(mSelectedEventOrNull);
        mDeleteCandidateOrNull = null;
        mSelectedEventOrNull = null;
        mErrorMessage = string.Empty;
        removeEventFromLocalCollections(deleteCandidate.CalendarID, deleteCandidate.ID);
        await InvokeAsync(StateHasChanged);
        try
        {
            auditLogID = await CalendarEventDeletionAuditLogService.RecordPendingDeletionAsync(currentUserSession, deleteCandidate, CancellationToken.None);
            await CalendarService.DeleteEventAsync(deleteCandidate.CalendarID, deleteCandidate.ID, CancellationToken.None);
        }
        catch (Exception exception)
        {
            if (auditLogID > 0)
            {
                await markDeletionFailedAsync(auditLogID, exception.Message);
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
            await CalendarEventDeletionAuditLogService.MarkDeletionSucceededAsync(auditLogID, CancellationToken.None);
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

    private async Task markDeletionFailedAsync(int auditLogID, string errorMessage)
    {
        try
        {
            await CalendarEventDeletionAuditLogService.MarkDeletionFailedAsync(auditLogID, errorMessage, CancellationToken.None);
        }
        catch
        {
        }
    }

    private string formatEditorDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
