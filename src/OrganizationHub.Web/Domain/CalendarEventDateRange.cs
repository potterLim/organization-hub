using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct CalendarEventDateRange
{
    public CalendarEventDateRange(DateTime start, DateTime end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Calendar event date range end cannot be earlier than start.");
        }

        Start = start;
        End = end;
    }

    public DateTime Start
    {
        get;
    }

    public DateTime End
    {
        get;
    }
}
