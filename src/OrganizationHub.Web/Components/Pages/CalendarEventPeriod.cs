using System;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

internal readonly record struct CalendarEventPeriod
{
    public DateTime StartLocal { get; }
    public DateTime EndExclusiveLocal { get; }
    public bool IsAllDay { get; }

    private CalendarEventPeriod(DateTime startLocal, DateTime endExclusiveLocal, bool isAllDay)
    {
        StartLocal = startLocal;
        EndExclusiveLocal = endExclusiveLocal;
        IsAllDay = isAllDay;
    }

    internal static CalendarEventPeriod? createOrNull(CalendarEventItem item, TimeZoneInfo timeZone)
    {
        if (item.Start is not DateTimeOffset start)
        {
            return null;
        }

        // Google all-day dates are floating dates; its end date is exclusive.
        DateTime startLocal = item.IsAllDay ? start.Date : TimeZoneInfo.ConvertTime(start, timeZone).DateTime;
        DateTime endLocal = item.End is DateTimeOffset end
            ? item.IsAllDay ? end.Date : TimeZoneInfo.ConvertTime(end, timeZone).DateTime
            : startLocal;
        return new CalendarEventPeriod(startLocal, endLocal, item.IsAllDay);
    }

    internal CalendarEditorSchedule createEditorSchedule()
    {
        DateTime endLocal = IsAllDay && EndExclusiveLocal > StartLocal ? EndExclusiveLocal.AddDays(-1) : EndExclusiveLocal;
        return CalendarEditorSchedule.CreateNormalized(StartLocal, endLocal);
    }

    internal bool overlaps(DateTime rangeStart, DateTime rangeEndExclusive)
    {
        if (EndExclusiveLocal <= StartLocal)
        {
            return StartLocal >= rangeStart && StartLocal < rangeEndExclusive;
        }

        return StartLocal < rangeEndExclusive && EndExclusiveLocal > rangeStart;
    }
}
