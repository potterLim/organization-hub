using Microsoft.AspNetCore.Components;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminGoogleDrivePanelActions
{
    public EventCallback<GoogleWorkspaceAssignmentTargetKey> SelectedFolderTargetKeyChanged
    {
        get; set;
    }

    public EventCallback EnsureStandardFoldersRequested
    {
        get; set;
    }

    public EventCallback ApplyStandardFolderNamesRequested
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

    public EventCallback<GoogleDriveFolderItem> OpenFolderRequested
    {
        get; set;
    }

    public EventCallback<GoogleDriveFolderItem> AssignFolderRequested
    {
        get; set;
    }
}
