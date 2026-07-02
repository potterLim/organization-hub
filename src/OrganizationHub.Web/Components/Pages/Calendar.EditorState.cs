using System;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class Calendar
{
    private void openCreateEditor()
    {
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
        mEditorCalendarId = mWritableCalendars.Count > 0 ? mWritableCalendars[0].CalendarId.GetRequiredValue() : string.Empty;
    }

    private void openEditEditor(CalendarEventItem calendarEvent)
    {
        mIsEditorOpen = true;
        mIsEditing = true;
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mEditorEventId = calendarEvent.Id;
        mEditorCalendarId = calendarEvent.CalendarId;
        mEditorSummary = calendarEvent.Summary;
        mEditorDescription = calendarEvent.Description;
        mEditorLocation = calendarEvent.Location;
        mIsEditorAllDay = calendarEvent.IsAllDay;
        DateTime clientNow = getClientNow();
        DateTime startLocal = calendarEvent.Start is DateTimeOffset start ? createClientDateTime(start) : clientNow;
        DateTime endLocal = calendarEvent.End is DateTimeOffset end ? createClientDateTime(end) : clientNow.AddHours(1);
        setEditorSchedule(CalendarEditorSchedule.CreateNormalized(startLocal, endLocal));
        mEditorMessage = string.Empty;
    }

    private void closeEditor()
    {
        mIsEditorOpen = false;
        mOpenEditorDateTimeFieldOrNull = null;
        mEditorMessage = string.Empty;
    }

    private void selectEvent(CalendarEventItem calendarEvent)
    {
        mSelectedEventOrNull = calendarEvent;
        mSelectedDayOrNull = getEventLocalDateOrNull(calendarEvent);
        mIsEditorOpen = false;
        mOpenEditorDateTimeFieldOrNull = null;
        mEditorMessage = string.Empty;
    }

    private void clearSelectedEvent()
    {
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
