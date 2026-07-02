using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private sealed class DriveFolderCollectionContext
    {
        public UserSession UserSession
        {
            get; private init;
        }

        public EDriveFolderAccessMode AccessMode
        {
            get; private init;
        }

        public EDriveFolderKind? FolderKindOrNull
        {
            get; private init;
        }

        public DriveFolderCollectionContext(
            UserSession userSession,
            EDriveFolderAccessMode accessMode,
            EDriveFolderKind? folderKindOrNull)
        {
            UserSession = userSession;
            AccessMode = accessMode;
            FolderKindOrNull = folderKindOrNull;
        }
    }

    private sealed class DriveFolderRegistration
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

        public OrganizationTeamId? TeamIdOrNull
        {
            get; private init;
        }

        public OrganizationTeamKey TeamKey
        {
            get; private init;
        }

        public string TeamName
        {
            get; private init;
        }

        public DriveFolderAccess Access
        {
            get; private init;
        }

        public DriveFolderRegistration(
            DriveFolderId folderId,
            string displayName,
            EDriveFolderKind folderKind,
            OrganizationTeamId? teamIdOrNull,
            OrganizationTeamKey teamKey,
            string teamName,
            DriveFolderAccess access)
        {
            FolderId = folderId;
            DisplayName = displayName;
            FolderKind = folderKind;
            TeamIdOrNull = teamIdOrNull;
            TeamKey = teamKey;
            TeamName = teamName;
            Access = access;
        }
    }

    private sealed class DriveFolderRegistrationRequest
    {
        public string? FolderIdOrNull
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

        public OrganizationTeamId? TeamIdOrNull
        {
            get; private init;
        }

        public OrganizationTeamKey TeamKey
        {
            get; private init;
        }

        public string TeamName
        {
            get; private init;
        }

        public DriveFolderAccess Access
        {
            get; private init;
        }

        public DriveFolderRegistrationRequest(
            string? folderIdOrNull,
            string displayName,
            EDriveFolderKind folderKind,
            OrganizationTeamId? teamIdOrNull,
            OrganizationTeamKey teamKey,
            string teamName,
            DriveFolderAccess access)
        {
            FolderIdOrNull = folderIdOrNull;
            DisplayName = displayName;
            FolderKind = folderKind;
            TeamIdOrNull = teamIdOrNull;
            TeamKey = teamKey;
            TeamName = teamName;
            Access = access;
        }
    }

    private sealed class DriveFolderAccess
    {
        public EDriveFolderReadAccess ReadAccess
        {
            get; private init;
        }

        public EDriveFolderUploadAccess UploadAccess
        {
            get; private init;
        }

        public DriveFolderAccess(EDriveFolderReadAccess readAccess, EDriveFolderUploadAccess uploadAccess)
        {
            ReadAccess = readAccess;
            UploadAccess = uploadAccess;
        }
    }

    private enum EDriveFolderReadAccess
    {
        ReadDenied,
        ReadAllowed
    }
}
