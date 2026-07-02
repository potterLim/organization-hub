using System;

namespace OrganizationHub.Web.Components.Pages;

internal readonly record struct CalendarDateTimeDayCell(
    DateTime Date,
    bool IsCurrentMonth,
    bool IsToday,
    bool IsSelected,
    bool IsDisabled);
