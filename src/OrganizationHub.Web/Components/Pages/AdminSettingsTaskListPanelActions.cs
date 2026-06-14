using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminSettingsTaskListPanelActions
{
    public EventCallback<GoogleWorkspaceAssignmentTargetKey> SelectedTaskListTargetKeyChanged
    {
        get; set;
    }

    public EventCallback RefreshRequested
    {
        get; set;
    }

    public EventCallback<GoogleTaskListItem> AssignTaskListRequested
    {
        get; set;
    }
}
