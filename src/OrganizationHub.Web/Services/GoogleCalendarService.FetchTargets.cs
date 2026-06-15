using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private void addCalendarFetchTarget(
        List<CalendarFetchTarget> fetchTargets,
        HashSet<string> fetchedCalendarIds,
        string? calendarIdOrNull,
        string calendarNameText
    )
    {
        string calendarIdValue = string.Empty;
        if (string.IsNullOrWhiteSpace(calendarIdOrNull) == false)
        {
            calendarIdValue = calendarIdOrNull;
        }

        CalendarId calendarId = CalendarId.CreateOptional(calendarIdValue);
        if (calendarId.HasValue == false)
        {
            return;
        }

        if (fetchedCalendarIds.Add(calendarId.GetRequiredValue()) == false)
        {
            return;
        }

        fetchTargets.Add(new CalendarFetchTarget(calendarId, CalendarName.CreateOptional(calendarNameText)));
    }

    private sealed class CalendarFetchTarget
    {
        public CalendarId CalendarId
        {
            get; private init;
        }
        public CalendarName CalendarName
        {
            get; private init;
        }

        public CalendarFetchTarget(CalendarId calendarId, CalendarName calendarName)
        {
            CalendarId = calendarId;
            CalendarName = calendarName;
        }
    }
}
