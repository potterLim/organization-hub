using System;
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
        mEditorEventId = string.Empty;
        mEditorSummary = string.Empty;
        mEditorDescription = string.Empty;
        mEditorLocation = string.Empty;
        setEditorSchedule(nextStart, nextStart.AddHours(1));
        mEditorIsAllDay = false;
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
        mEditorIsAllDay = calendarEvent.IsAllDay;
        DateTime startLocal = calendarEvent.Start is DateTimeOffset start ? start.ToLocalTime().DateTime : DateTime.Now;
        DateTime endLocal = calendarEvent.End is DateTimeOffset end ? end.ToLocalTime().DateTime : DateTime.Now.AddHours(1);
        setEditorSchedule(startLocal, endLocal);
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

    private void setEditorSchedule(DateTime startLocal, DateTime endLocal)
    {
        mEditorStartLocalOrNull = startLocal;
        mEditorEndLocalOrNull = endLocal < startLocal ? startLocal : endLocal;
        mOpenEditorDateTimeFieldOrNull = null;
    }
}
