using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class DriveFolderReference
{
    public DriveFolderId FolderId
    {
        get; private init;
    }
    public string DisplayName { get; private init; } = string.Empty;
    public EDriveFolderKind FolderKind
    {
        get; private init;
    }
    public OrganizationTeamId? TeamIdOrNull
    {
        get; private init;
    }
    public int? TeamIdValueOrNull
    {
        get
        {
            return OrganizationTeamId.GetValueOrNull(TeamIdOrNull);
        }
    }
    public OrganizationTeamKey TeamKey
    {
        get; private init;
    }
    public string TeamKeyValue
    {
        get
        {
            return TeamKey.Value;
        }
    }
    public string TeamName { get; private init; } = string.Empty;
    public bool CanUpload
    {
        get; private init;
    }
    public bool IsManagementTeam
    {
        get
        {
            if (TeamKey.IsManagementTeamKey)
            {
                return true;
            }

            return string.Equals(TeamName, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME, StringComparison.OrdinalIgnoreCase);
        }
    }

    public DriveFolderReference(DriveFolderReferenceCreationRequest request)
    {
        FolderId = request.FolderId;
        DisplayName = request.DisplayName;
        FolderKind = request.FolderKind;
        TeamIdOrNull = request.TeamIdOrNull;
        TeamKey = request.TeamKey;
        TeamName = request.TeamName;
        CanUpload = request.UploadAccess == EDriveFolderUploadAccess.UploadAllowed;
    }
}
