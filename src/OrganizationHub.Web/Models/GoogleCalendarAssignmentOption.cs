using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class GoogleCalendarAssignmentOption
{
    public string TargetKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EGoogleCalendarAssignmentKind CalendarAssignmentKind
    {
        get; set;
    }
    public int? TeamID
    {
        get; set;
    }
}
