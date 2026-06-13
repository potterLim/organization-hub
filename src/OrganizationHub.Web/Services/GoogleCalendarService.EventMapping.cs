using System;
using System.Collections.Generic;
using Google.Apis.Calendar.v3.Data;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private static IReadOnlyList<string> createCalendarChangeKeys(string calendarID)
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.CALENDAR);
        keys.Add(ChangeVersionKeys.CreateCalendarKey(calendarID));
        return keys;
    }

    private Event createGoogleEvent(CalendarEventFormModel formModel)
    {
        Event googleEvent = new Event();
        googleEvent.Summary = formModel.Summary.Trim();
        googleEvent.Description = getEmptyAsNull(formModel.Description);
        googleEvent.Location = getEmptyAsNull(formModel.Location);
        googleEvent.Start = createEventDateTime(formModel, ECalendarEventDateKind.Start);
        googleEvent.End = createEventDateTime(formModel, ECalendarEventDateKind.End);
        return googleEvent;
    }

    private EventDateTime createEventDateTime(CalendarEventFormModel formModel, ECalendarEventDateKind dateKind)
    {
        EventDateTime eventDateTime = new EventDateTime();
        DateTime localDateTime = dateKind == ECalendarEventDateKind.Start ? formModel.StartLocal : formModel.EndLocal;
        if (formModel.IsAllDay)
        {
            DateTime date = localDateTime.Date;
            if (dateKind == ECalendarEventDateKind.End)
            {
                date = date.AddDays(1);
            }

            eventDateTime.Date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return eventDateTime;
        }

        eventDateTime.DateTimeDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), getCalendarUtcOffset(localDateTime));
        eventDateTime.TimeZone = getCalendarTimeZoneID();
        return eventDateTime;
    }

    private TimeSpan getCalendarUtcOffset(DateTime localDateTime)
    {
        try
        {
            TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(getCalendarTimeZoneID());
            return timeZoneInfo.GetUtcOffset(localDateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        }
    }

    private string getCalendarTimeZoneID()
    {
        if (string.IsNullOrWhiteSpace(mOptions.CalendarTimeZone))
        {
            return "Asia/Seoul";
        }

        return mOptions.CalendarTimeZone;
    }

    private IReadOnlyList<CalendarEventItem> createCalendarEventItems(Events events, string calendarID, string calendarName)
    {
        List<CalendarEventItem> items = new List<CalendarEventItem>();
        if (events.Items == null)
        {
            return items;
        }

        foreach (Event googleEvent in events.Items)
        {
            CalendarEventItem item = createCalendarEventItem(googleEvent, calendarID, calendarName);
            items.Add(item);
        }

        return items;
    }

    private IReadOnlyList<CalendarEventItem> cloneCalendarEvents(IReadOnlyList<CalendarEventItem> calendarEvents)
    {
        List<CalendarEventItem> clonedEvents = new List<CalendarEventItem>();
        foreach (CalendarEventItem calendarEvent in calendarEvents)
        {
            clonedEvents.Add(cloneCalendarEvent(calendarEvent));
        }

        return clonedEvents;
    }

    private CalendarEventItem cloneCalendarEvent(CalendarEventItem calendarEvent)
    {
        CalendarEventItem clonedEvent = new CalendarEventItem();
        clonedEvent.ID = calendarEvent.ID;
        clonedEvent.CalendarID = calendarEvent.CalendarID;
        clonedEvent.CalendarName = calendarEvent.CalendarName;
        clonedEvent.Summary = calendarEvent.Summary;
        clonedEvent.Description = calendarEvent.Description;
        clonedEvent.Location = calendarEvent.Location;
        clonedEvent.Start = calendarEvent.Start;
        clonedEvent.End = calendarEvent.End;
        clonedEvent.IsAllDay = calendarEvent.IsAllDay;
        clonedEvent.CanEdit = calendarEvent.CanEdit;
        clonedEvent.CreatedByDisplayName = calendarEvent.CreatedByDisplayName;
        clonedEvent.CreatedAt = calendarEvent.CreatedAt;
        return clonedEvent;
    }

    private CalendarEventItem createCalendarEventItem(Event googleEvent, string calendarID, string calendarName)
    {
        CalendarEventItem item = new CalendarEventItem();
        item.ID = getNullAsEmpty(googleEvent.Id);
        item.CalendarID = calendarID;
        item.CalendarName = calendarName;
        item.Summary = getNullAsEmpty(googleEvent.Summary);
        item.Description = getNullAsEmpty(googleEvent.Description);
        item.Location = getNullAsEmpty(googleEvent.Location);
        item.Start = getEventTimeOrNull(googleEvent.Start);
        item.End = getEventTimeOrNull(googleEvent.End);
        item.IsAllDay = googleEvent.Start != null && string.IsNullOrWhiteSpace(googleEvent.Start.Date) == false;
        return item;
    }

    private DateTimeOffset? getEventTimeOrNull(EventDateTime eventDateTimeOrNull)
    {
        if (eventDateTimeOrNull == null)
        {
            return null;
        }

        if (eventDateTimeOrNull.DateTimeDateTimeOffset.HasValue)
        {
            return eventDateTimeOrNull.DateTimeDateTimeOffset.Value;
        }

        if (string.IsNullOrWhiteSpace(eventDateTimeOrNull.Date) == false)
        {
            DateTime dateTime;
            bool isParsed = DateTime.TryParse(eventDateTimeOrNull.Date, out dateTime);
            if (isParsed)
            {
                return new DateTimeOffset(dateTime);
            }
        }

        return null;
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }

    private string? getEmptyAsNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
