using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class GoogleWorkspaceTeamFolderNameContext
{
    public GoogleWorkspaceTeamFolderNameContext(Team team, string teamParentFolderID, DriveFileMetadata? materialsMetadataOrNull, DriveFileMetadata? approvedMetadataOrNull)
    {
        Team = team;
        TeamParentFolderID = teamParentFolderID;
        MaterialsMetadataOrNull = materialsMetadataOrNull;
        ApprovedMetadataOrNull = approvedMetadataOrNull;
    }

    public Team Team
    {
        get; private init;
    }

    public string TeamParentFolderID
    {
        get; private init;
    }

    public DriveFileMetadata? MaterialsMetadataOrNull
    {
        get; private init;
    }

    public DriveFileMetadata? ApprovedMetadataOrNull
    {
        get; private init;
    }
}
