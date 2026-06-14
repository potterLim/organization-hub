using System;
using System.Globalization;
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
        mEditorStartText = formatEditorDateTime(nextStart);
        mEditorEndText = formatEditorDateTime(nextStart.AddHours(1));
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

    private Task handleEditorCalendarChangedAsync(string calendarId)
    {
        mEditorCalendarId = calendarId;
        return Task.CompletedTask;
    }

    private string formatEditorDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
