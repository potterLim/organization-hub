using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private void addCalendarFetchTarget(
        List<CalendarFetchTarget> fetchTargets,
        HashSet<string> fetchedCalendarIds,
        string? calendarIdOrNull,
        string calendarName
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

        fetchTargets.Add(new CalendarFetchTarget(calendarId, calendarName));
    }

    private sealed class CalendarFetchTarget
    {
        public CalendarId CalendarId
        {
            get; private init;
        }
        public string CalendarName
        {
            get; private init;
        }

        public CalendarFetchTarget(CalendarId calendarId, string calendarName)
        {
            CalendarId = calendarId;
            CalendarName = calendarName;
        }
    }
}
