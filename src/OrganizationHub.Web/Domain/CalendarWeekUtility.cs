using System;

namespace OrganizationHub.Web.Domain;

public static class CalendarWeekUtility
{
    public static DateTime GetStartOfWeek(DateTime date)
    {
        int dayOffset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-dayOffset);
    }

    public static DateTime GetEndOfWeek(DateTime date)
    {
        return GetStartOfWeek(date).AddDays(7);
    }
}
