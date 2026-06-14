using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task addGlobalFoldersAsync(
        List<DriveFolderReference> folders,
        GlobalSetting setting,
        DriveFolderCollectionContext collectionContext,
        CancellationToken cancellationToken)
    {
        UserSession userSession = collectionContext.UserSession;
        EPermissionFallback publicUploadFallback = PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanUploadPublicMaterials(userSession.Role));
        EPermissionFallback commonUploadFallback = PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanUploadCommonMaterials(userSession.Role));
        EPermissionFallback workResourcesUploadFallback = PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanUploadWorkResources(userSession.Role));

        bool canReadGlobalFiles = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_READ),
            null,
            collectionContext.ConfigurationMode,
            EPermissionFallback.Allow,
            cancellationToken);
        bool canUploadPublic = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_UPLOAD),
            null,
            collectionContext.ConfigurationMode,
            publicUploadFallback,
            cancellationToken);
        bool canUploadCommon = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_UPLOAD),
            null,
            collectionContext.ConfigurationMode,
            commonUploadFallback,
            cancellationToken);
        bool canUploadWorkResources = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_UPLOAD),
            null,
            collectionContext.ConfigurationMode,
            workResourcesUploadFallback,
            cancellationToken);
        EDriveFolderReadAccess globalReadAccess = canReadGlobalFiles ? EDriveFolderReadAccess.ReadAllowed : EDriveFolderReadAccess.ReadDenied;
        EDriveFolderUploadAccess publicUploadAccess = canUploadPublic ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        EDriveFolderUploadAccess commonUploadAccess = canUploadCommon ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        EDriveFolderUploadAccess workResourcesUploadAccess = canUploadWorkResources ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;

        DriveFolderRegistration publicFolder = createGlobalDriveFolderRegistration(
            setting.PublicFolderId,
            OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME,
            EDriveFolderKind.Public,
            createDriveFolderAccess(globalReadAccess, publicUploadAccess));
        addFolderIfPermitted(folders, publicFolder, collectionContext);

        DriveFolderRegistration commonFolder = createGlobalDriveFolderRegistration(
            setting.CommonFolderId,
            OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME,
            EDriveFolderKind.Common,
            createDriveFolderAccess(globalReadAccess, commonUploadAccess));
        addFolderIfPermitted(folders, commonFolder, collectionContext);

        DriveFolderRegistration workResourcesFolder = createGlobalDriveFolderRegistration(
            setting.WorkResourcesFolderId,
            OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME,
            EDriveFolderKind.WorkResources,
            createDriveFolderAccess(globalReadAccess, workResourcesUploadAccess));
        addFolderIfPermitted(folders, workResourcesFolder, collectionContext);

        EPermissionFallback responsibleReadFallback = PermissionFallbackUtility.FromDecision(userSession.IsTeamLeaderOrHigherRole());
        bool canReadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_READ),
            null,
            collectionContext.ConfigurationMode,
            responsibleReadFallback,
            cancellationToken);
        if (canReadResponsibleFolder)
        {
            EPermissionFallback responsibleUploadFallback = PermissionFallbackUtility.FromDecision(UserPermissionPolicy.CanUploadResponsibleResources(userSession.Role));
            bool canUploadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(
                userSession,
                new PermissionKey(PermissionKeys.FILES_UPLOAD),
                null,
                collectionContext.ConfigurationMode,
                responsibleUploadFallback,
                cancellationToken);
            EDriveFolderReadAccess responsibleReadAccess = canReadResponsibleFolder ? EDriveFolderReadAccess.ReadAllowed : EDriveFolderReadAccess.ReadDenied;
            EDriveFolderUploadAccess responsibleUploadAccess = canUploadResponsibleFolder ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
            DriveFolderRegistration responsibleFolder = createGlobalDriveFolderRegistration(
                setting.ResponsibleResourcesFolderId,
                OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME,
                EDriveFolderKind.ResponsibleResources,
                createDriveFolderAccess(responsibleReadAccess, responsibleUploadAccess));
            addFolderIfPermitted(folders, responsibleFolder, collectionContext);
        }
    }

    private async Task addTeamFoldersAsync(
        List<DriveFolderReference> folders,
        Team team,
        DriveFolderCollectionContext collectionContext,
        CancellationToken cancellationToken)
    {
        UserSession userSession = collectionContext.UserSession;
        OrganizationTeamId teamId = OrganizationTeamId.CreateExisting(team.Id);
        EPermissionFallback teamReadFallback = PermissionFallbackUtility.FromDecision(canReadMaterialsForTeam(userSession, team));
        EPermissionFallback teamUploadFallback = PermissionFallbackUtility.FromDecision(canUploadMaterialsForTeam(userSession, team));
        bool canReadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_READ),
            teamId,
            collectionContext.ConfigurationMode,
            teamReadFallback,
            cancellationToken);
        bool canUploadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(
            userSession,
            new PermissionKey(PermissionKeys.FILES_UPLOAD),
            teamId,
            collectionContext.ConfigurationMode,
            teamUploadFallback,
            cancellationToken);
        EDriveFolderReadAccess teamReadAccess = canReadTeamFiles ? EDriveFolderReadAccess.ReadAllowed : EDriveFolderReadAccess.ReadDenied;
        EDriveFolderUploadAccess teamUploadAccess = canUploadTeamFiles ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;

        DriveFolderRegistration teamMaterialsFolder = createTeamDriveFolderRegistration(
            team,
            team.MaterialsFolderId,
            team.DisplayName + " 자료",
            EDriveFolderKind.TeamMaterials,
            createDriveFolderAccess(teamReadAccess, teamUploadAccess));
        addFolderIfPermitted(folders, teamMaterialsFolder, collectionContext);

        DriveFolderRegistration teamApprovedFolder = createTeamDriveFolderRegistration(
            team,
            team.ApprovedFolderId,
            team.DisplayName + " 결재 완료 자료",
            EDriveFolderKind.TeamApproved,
            createDriveFolderAccess(teamReadAccess, EDriveFolderUploadAccess.UploadDenied));
        addFolderIfPermitted(folders, teamApprovedFolder, collectionContext);
    }

    private DriveFolderRegistration createGlobalDriveFolderRegistration(
        string? folderIdOrNull,
        string displayName,
        EDriveFolderKind folderKind,
        DriveFolderAccess access)
    {
        DriveFolderRegistrationRequest request = new DriveFolderRegistrationRequest(
            folderIdOrNull,
            displayName,
            folderKind,
            null,
            OrganizationTeamKey.Empty,
            string.Empty,
            access);
        return createDriveFolderRegistration(request);
    }

    private DriveFolderRegistration createTeamDriveFolderRegistration(
        Team team,
        string? folderIdOrNull,
        string displayName,
        EDriveFolderKind folderKind,
        DriveFolderAccess access)
    {
        DriveFolderRegistrationRequest request = new DriveFolderRegistrationRequest(
            folderIdOrNull,
            displayName,
            folderKind,
            OrganizationTeamId.CreateExisting(team.Id),
            new OrganizationTeamKey(team.TeamKey),
            team.DisplayName,
            access);
        return createDriveFolderRegistration(request);
    }

    private DriveFolderRegistration createDriveFolderRegistration(DriveFolderRegistrationRequest request)
    {
        return new DriveFolderRegistration(
            createDriveFolderId(request.FolderIdOrNull),
            request.DisplayName,
            request.FolderKind,
            request.TeamIdOrNull,
            request.TeamKey,
            request.TeamName,
            request.Access);
    }

    private DriveFolderAccess createDriveFolderAccess(EDriveFolderReadAccess readAccess, EDriveFolderUploadAccess uploadAccess)
    {
        return new DriveFolderAccess(readAccess, uploadAccess);
    }

    private DriveFolderId createDriveFolderId(string? folderIdOrNull)
    {
        if (string.IsNullOrWhiteSpace(folderIdOrNull))
        {
            return DriveFolderId.Empty;
        }

        return DriveFolderId.CreateOptional(folderIdOrNull);
    }

    private void addFolderIfConfigured(
        List<DriveFolderReference> folders,
        DriveFolderRegistration folderRegistration,
        EDriveFolderUploadAccess uploadAccess,
        EDriveFolderKind? folderKindOrNull
    )
    {
        if (folderRegistration.FolderId.HasValue == false)
        {
            return;
        }

        if (folderKindOrNull.HasValue && folderKindOrNull.Value != folderRegistration.FolderKind)
        {
            return;
        }

        DriveFolderOwnerContext ownerContext = new DriveFolderOwnerContext(
            folderRegistration.TeamIdOrNull,
            folderRegistration.TeamKey,
            folderRegistration.TeamName);

        DriveFolderReferenceCreationRequest request = new DriveFolderReferenceCreationRequest(
            folderRegistration.FolderId,
            folderRegistration.DisplayName,
            folderRegistration.FolderKind,
            uploadAccess,
            ownerContext);
        folders.Add(new DriveFolderReference(request));
    }

    private void addFolderIfPermitted(
        List<DriveFolderReference> folders,
        DriveFolderRegistration folderRegistration,
        DriveFolderCollectionContext collectionContext
    )
    {
        if (collectionContext.AccessMode == EDriveFolderAccessMode.Uploadable && folderRegistration.Access.UploadAccess == EDriveFolderUploadAccess.UploadDenied)
        {
            return;
        }

        if (collectionContext.AccessMode != EDriveFolderAccessMode.Uploadable && folderRegistration.Access.ReadAccess == EDriveFolderReadAccess.ReadDenied && folderRegistration.Access.UploadAccess == EDriveFolderUploadAccess.UploadDenied)
        {
            return;
        }

        addFolderIfConfigured(folders, folderRegistration, folderRegistration.Access.UploadAccess, collectionContext.FolderKindOrNull);
    }

}
