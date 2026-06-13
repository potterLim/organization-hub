using System.Collections.Generic;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleCalendarService
{
    private void addCalendarFetchTarget(List<CalendarFetchTarget> fetchTargets, HashSet<string> fetchedCalendarIDs, string? calendarIDOrNull, string calendarName)
    {
        if (string.IsNullOrWhiteSpace(calendarIDOrNull))
        {
            return;
        }

        if (fetchedCalendarIDs.Add(calendarIDOrNull) == false)
        {
            return;
        }

        fetchTargets.Add(new CalendarFetchTarget(calendarIDOrNull, calendarName));
    }

    private sealed class CalendarFetchTarget
    {
        public string CalendarID
        {
            get; private init;
        }
        public string CalendarName
        {
            get; private init;
        }

        public CalendarFetchTarget(string calendarID, string calendarName)
        {
            CalendarID = calendarID;
            CalendarName = calendarName;
        }
    }
}
