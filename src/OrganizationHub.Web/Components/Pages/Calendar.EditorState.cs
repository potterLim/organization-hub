using System;
using System.Linq;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private void openCreateEditor()
    {
        if (mIsEditorOpen)
        {
            return;
        }

        DateTime clientNow = getClientNow();
        DateTime nextStart = clientNow.Date.AddHours(clientNow.Hour + 1);
        if (nextStart <= clientNow)
        {
            nextStart = nextStart.AddHours(1);
        }

        mIsEditorOpen = true;
        mIsEditing = false;
        mSelectedEventOrNull = null;
        mSelectedDayOrNull = null;
        mEditorEventId = string.Empty;
        mEditorSummary = string.Empty;
        mEditorDescription = string.Empty;
        mEditorLocation = string.Empty;
        setEditorSchedule(CalendarEditorSchedule.CreateNormalized(nextStart, nextStart.AddHours(1)));
        mIsEditorAllDay = false;
        mEditorMessage = string.Empty;
        mHasEditConflict = false;
        mEditorCalendarId = mCreatableCalendars.Count > 0 ? mCreatableCalendars[0].CalendarId.GetRequiredValue() : string.Empty;
        rememberEditorDraft();
    }

    private void openEditEditor(CalendarEventItem calendarEvent)
    {
        if (mIsEditorOpen || calendarEvent.CanEdit == false)
        {
            return;
        }

        mIsEditorOpen = true;
        mIsEditing = true;
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mEditorEventId = calendarEvent.Id;
        mEditorETag = calendarEvent.ETag;
        mHasEditConflict = false;
        mEditorCalendarId = calendarEvent.CalendarId;
        mEditorSummary = calendarEvent.Summary;
        mEditorDescription = calendarEvent.Description;
        mEditorLocation = calendarEvent.Location;
        mIsEditorAllDay = calendarEvent.IsAllDay;
        DateTime clientNow = getClientNow();
        CalendarEventPeriod? periodOrNull = CalendarEventPeriod.createOrNull(calendarEvent, mClientTimeZoneInfo);
        setEditorSchedule(periodOrNull?.createEditorSchedule() ?? CalendarEditorSchedule.CreateNormalized(clientNow, clientNow.AddHours(1)));
        mEditorMessage = string.Empty;
        rememberEditorDraft();
    }

    private void closeEditor()
    {
        mIsDiscardConfirmationOpen = false;
        mIsEditorOpen = false;
        mOpenEditorDateTimeFieldOrNull = null;
        mEditorMessage = string.Empty;
        mHasEditConflict = false;
    }

    private async Task reloadConflictedEventAsync()
    {
        mPreviousDraft = mEditorSummary + " · 위치: " + mEditorLocation + " · 설명: " + mEditorDescription + " · 시작: " + mEditorStartLocalOrNull?.ToString("yyyy-MM-dd HH:mm") + " · 종료: " + mEditorEndLocalOrNull?.ToString("yyyy-MM-dd HH:mm");
        string eventId = mEditorEventId;
        string calendarId = mEditorCalendarId;
        closeEditor();
        clearSelectedEvent();
        await loadEventsAsync();
        CalendarEventItem? latestOrNull = mMonthEvents.FirstOrDefault(item => item.Id == eventId && item.CalendarId == calendarId);
        if (latestOrNull != null)
        {
            openEditEditor(latestOrNull);
        }
    }

    private void selectEvent(CalendarEventItem calendarEvent)
    {
        if (mIsEditorOpen)
        {
            return;
        }

        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mIsEditorOpen = false;
        mOpenEditorDateTimeFieldOrNull = null;
        mEditorMessage = string.Empty;
    }

    private void clearSelectedEvent()
    {
        if (mIsEditorOpen)
        {
            return;
        }

        mSelectedEventOrNull = null;
        mIsEditorOpen = false;
        mOpenEditorDateTimeFieldOrNull = null;
        mEditorMessage = string.Empty;
    }

    private Task handleEditorCalendarChangedAsync(string calendarId)
    {
        mEditorCalendarId = calendarId;
        return Task.CompletedTask;
    }

    private Task handleEditorStartChangedAsync(DateTime? startLocalOrNull)
    {
        mEditorStartLocalOrNull = startLocalOrNull;
        if (startLocalOrNull is DateTime startLocal && (mEditorEndLocalOrNull == null || mEditorEndLocalOrNull < startLocal))
        {
            mEditorEndLocalOrNull = startLocal;
        }

        return Task.CompletedTask;
    }

    private Task handleEditorEndChangedAsync(DateTime? endLocalOrNull)
    {
        if (endLocalOrNull is DateTime endLocal && mEditorStartLocalOrNull is DateTime startLocal && endLocal < startLocal)
        {
            mEditorEndLocalOrNull = startLocal;
            return Task.CompletedTask;
        }

        mEditorEndLocalOrNull = endLocalOrNull;
        return Task.CompletedTask;
    }

    private Task handleEditorDateTimeFieldOpenChangedAsync(ECalendarEditorDateTimeField dateTimeField, bool isOpen)
    {
        mOpenEditorDateTimeFieldOrNull = isOpen ? dateTimeField : null;
        return Task.CompletedTask;
    }

    private void setEditorSchedule(CalendarEditorSchedule schedule)
    {
        mEditorStartLocalOrNull = schedule.StartLocal;
        mEditorEndLocalOrNull = schedule.EndLocal;
        mOpenEditorDateTimeFieldOrNull = null;
    }
}
