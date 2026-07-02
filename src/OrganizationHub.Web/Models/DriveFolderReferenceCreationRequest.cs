using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class DriveFolderReferenceCreationRequest
{
    public DriveFolderId FolderId
    {
        get; private init;
    }

    public string DisplayName
    {
        get; private init;
    }

    public EDriveFolderKind FolderKind
    {
        get; private init;
    }

    public EDriveFolderUploadAccess UploadAccess
    {
        get; private init;
    }

    public DriveFolderOwnerContext OwnerContext
    {
        get; private init;
    }

    public OrganizationTeamId? TeamIdOrNull
    {
        get
        {
            return OwnerContext.TeamIdOrNull;
        }
    }

    public OrganizationTeamKey TeamKey
    {
        get
        {
            return OwnerContext.TeamKey;
        }
    }

    public string TeamName
    {
        get
        {
            return OwnerContext.TeamName;
        }
    }

    public DriveFolderReferenceCreationRequest(
        DriveFolderId folderId,
        string displayName,
        EDriveFolderKind folderKind,
        EDriveFolderUploadAccess uploadAccess
    )
        : this(folderId, displayName, folderKind, uploadAccess, DriveFolderOwnerContext.Empty)
    {
    }

    public DriveFolderReferenceCreationRequest(
        DriveFolderId folderId,
        string displayName,
        EDriveFolderKind folderKind,
        EDriveFolderUploadAccess uploadAccess,
        DriveFolderOwnerContext ownerContext)
    {
        FolderId = folderId;
        DisplayName = displayName;
        FolderKind = folderKind;
        UploadAccess = uploadAccess;
        OwnerContext = ownerContext;
    }
}
