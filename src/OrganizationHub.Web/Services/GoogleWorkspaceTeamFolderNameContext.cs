using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class GoogleWorkspaceTeamFolderNameContext
{
    public GoogleWorkspaceTeamFolderNameContext(
        Team team,
        DriveFolderId teamParentFolderId,
        DriveFileMetadata? materialsMetadataOrNull,
        DriveFileMetadata? approvedMetadataOrNull
    )
    {
        Team = team;
        TeamParentFolderId = teamParentFolderId;
        MaterialsMetadataOrNull = materialsMetadataOrNull;
        ApprovedMetadataOrNull = approvedMetadataOrNull;
    }

    public Team Team
    {
        get; private init;
    }

    public DriveFolderId TeamParentFolderId
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
