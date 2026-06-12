using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFolderReference
{
    public string FolderID { get; private init; } = string.Empty;
    public string DisplayName { get; private init; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; private init;
    }
    public int? TeamID
    {
        get; private init;
    }
    public string TeamKey { get; private init; } = string.Empty;
    public string TeamName { get; private init; } = string.Empty;
    public bool CanUpload
    {
        get; private init;
    }
    public bool IsManagementTeam
    {
        get
        {
            if (string.Equals(TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(TeamName, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }

    public DriveFolderReference(string folderID, string displayName, EDriveFolderKind folderKind, int? teamID, string teamName, EDriveFolderUploadAccess uploadAccess)
        : this(folderID, displayName, folderKind, teamID, string.Empty, teamName, uploadAccess)
    {
    }

    public DriveFolderReference(string folderID, string displayName, EDriveFolderKind folderKind, int? teamID, string teamKey, string teamName, EDriveFolderUploadAccess uploadAccess)
    {
        FolderID = folderID;
        DisplayName = displayName;
        FolderKind = folderKind;
        TeamID = teamID;
        TeamKey = teamKey;
        TeamName = teamName;
        CanUpload = uploadAccess == EDriveFolderUploadAccess.UploadAllowed;
    }
}

