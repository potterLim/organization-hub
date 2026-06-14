using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminSettingsDrivePanelActions
{
    public EventCallback<GoogleWorkspaceAssignmentTargetKey> SelectedFolderTargetKeyChanged
    {
        get; set;
    }

    public EventCallback<ESettingFileAssignmentTarget> SelectedFileAssignmentTargetChanged
    {
        get; set;
    }

    public EventCallback DriveRootRequested
    {
        get; set;
    }

    public EventCallback OfficialRootRequested
    {
        get; set;
    }

    public EventCallback RefreshRequested
    {
        get; set;
    }

    public EventCallback<int> OpenPathRequested
    {
        get; set;
    }

    public EventCallback<DriveFileItem> OpenFolderRequested
    {
        get; set;
    }

    public EventCallback<DriveFileItem> AssignFolderRequested
    {
        get; set;
    }

    public EventCallback<DriveFileItem> AssignFileRequested
    {
        get; set;
    }
}
