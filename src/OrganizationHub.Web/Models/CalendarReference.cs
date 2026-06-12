using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class CalendarReference
{
    public string CalendarID { get; private init; } = string.Empty;
    public string DisplayName { get; private init; } = string.Empty;
    public ECalendarViewKind ViewKind
    {
        get; private init;
    }
    public int? TeamID
    {
        get; private init;
    }

    public CalendarReference(string calendarID, string displayName, ECalendarViewKind viewKind, int? teamID)
    {
        CalendarID = calendarID;
        DisplayName = displayName;
        ViewKind = viewKind;
        TeamID = teamID;
    }
}

