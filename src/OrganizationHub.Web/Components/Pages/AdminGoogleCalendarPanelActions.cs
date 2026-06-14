using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminGoogleCalendarPanelActions
{
    public EventCallback<GoogleWorkspaceAssignmentTargetKey> SelectedCalendarTargetKeyChanged
    {
        get; set;
    }

    public EventCallback EnsureStandardCalendarsRequested
    {
        get; set;
    }

    public EventCallback RefreshRequested
    {
        get; set;
    }

    public EventCallback<GoogleCalendarListItem> AssignCalendarRequested
    {
        get; set;
    }
}
