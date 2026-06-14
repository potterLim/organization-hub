using System;
using System.Collections.Generic;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public sealed class AdminSettingsDrivePanelState
{
    public GoogleConnectionStatus Status
    {
        get; set;
    } = new GoogleConnectionStatus();

    public bool HasOfficialRootFolder
    {
        get; set;
    }

    public IReadOnlyList<GoogleFolderAssignmentOption> FolderAssignmentOptions
    {
        get; set;
    } = new List<GoogleFolderAssignmentOption>();

    public GoogleWorkspaceAssignmentTargetKey SelectedFolderTargetKey
    {
        get; set;
    } = GoogleWorkspaceAssignmentTargetKey.Empty;

    public ESettingFileAssignmentTarget SelectedFileAssignmentTarget
    {
        get; set;
    } = ESettingFileAssignmentTarget.NoticeFile;

    public string SelectedFolderAssignmentDisplayName
    {
        get; set;
    } = string.Empty;

    public string SelectedFolderAssignmentDetail
    {
        get; set;
    } = string.Empty;

    public string SelectedFileAssignmentDisplayName
    {
        get; set;
    } = string.Empty;

    public string SelectedFileAssignmentDetail
    {
        get; set;
    } = string.Empty;

    public IReadOnlyList<GoogleDriveFolderPathItem> FolderPath
    {
        get; set;
    } = new List<GoogleDriveFolderPathItem>();

    public IReadOnlyList<DriveFileItem> DriveItems
    {
        get; set;
    } = new List<DriveFileItem>();

    public bool IsLoadingDriveItems
    {
        get; set;
    }

    public Func<DriveFileItem, string> GetFileTypeDisplayName
    {
        get; set;
    } = getDefaultFileTypeDisplayName;

    public Func<DateTime?, string> FormatDate
    {
        get; set;
    } = getDefaultDateText;

    private static string getDefaultFileTypeDisplayName(DriveFileItem driveItem)
    {
        return string.Empty;
    }

    private static string getDefaultDateText(DateTime? dateTimeOrNull)
    {
        return "-";
    }
}
