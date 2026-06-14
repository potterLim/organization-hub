using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminGoogleDrivePanelState
{
    public GoogleConnectionStatus Status
    {
        get; set;
    } = new GoogleConnectionStatus();

    public IReadOnlyList<GoogleFolderAssignmentOption> FolderAssignmentOptions
    {
        get; set;
    } = new List<GoogleFolderAssignmentOption>();

    public GoogleWorkspaceAssignmentTargetKey SelectedFolderTargetKey
    {
        get; set;
    } = GoogleWorkspaceAssignmentTargetKey.Empty;

    public IReadOnlyList<GoogleDriveFolderPathItem> FolderPath
    {
        get; set;
    } = new List<GoogleDriveFolderPathItem>();

    public IReadOnlyList<GoogleDriveFolderItem> Folders
    {
        get; set;
    } = new List<GoogleDriveFolderItem>();

    public bool IsEnsuringStandardFolders
    {
        get; set;
    }

    public bool IsApplyingStandardFolderNames
    {
        get; set;
    }

    public Func<DateTime?, string> FormatDate
    {
        get; set;
    } = getDefaultDateText;

    private static string getDefaultDateText(DateTime? dateTimeOrNull)
    {
        return "-";
    }
}
