using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class CalendarReference
{
    public CalendarId CalendarId
    {
        get; private init;
    }
    public string DisplayName { get; private init; } = string.Empty;
    public ECalendarViewKind ViewKind
    {
        get; private init;
    }
    public OrganizationTeamId? TeamIdOrNull
    {
        get; private init;
    }

    public CalendarReference(CalendarId calendarId, string displayName, ECalendarViewKind viewKind, OrganizationTeamId? teamIdOrNull)
    {
        CalendarId = calendarId;
        DisplayName = displayName;
        ViewKind = viewKind;
        TeamIdOrNull = teamIdOrNull;
    }
}
