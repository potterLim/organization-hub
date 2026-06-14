using System;
using System.Collections.Generic;

namespace OrganizationHub.Web.Models;

public sealed class CalendarDayCell
{
    public DateTime Date
    {
        get; set;
    }

    public bool IsCurrentMonth
    {
        get; set;
    }

    public bool IsToday
    {
        get; set;
    }

    public List<CalendarEventItem> Events { get; set; } = new List<CalendarEventItem>();
}
