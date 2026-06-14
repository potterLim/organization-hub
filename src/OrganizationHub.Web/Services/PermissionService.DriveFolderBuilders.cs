using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task addGlobalFoldersAsync(List<DriveFolderReference> folders, GlobalSetting setting, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadGlobalFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_READ), null, usesConfiguredPermissions, true, cancellationToken);
        bool canUploadPublic = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadPublicMaterials(userSession.Role), cancellationToken);
        bool canUploadCommon = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadCommonMaterials(userSession.Role), cancellationToken);
        bool canUploadWorkResources = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadWorkResources(userSession.Role), cancellationToken);
        addFolderIfPermitted(folders, setting.PublicFolderID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, EDriveFolderKind.Public, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadPublic, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.CommonFolderID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, EDriveFolderKind.Common, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadCommon, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.WorkResourcesFolderID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, EDriveFolderKind.WorkResources, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadWorkResources, accessMode, folderKindOrNull);
        bool canReadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_READ), null, usesConfiguredPermissions, userSession.IsTeamLeaderOrHigherRole(), cancellationToken);
        if (canReadResponsibleFolder)
        {
            bool canUploadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadResponsibleResources(userSession.Role), cancellationToken);
            addFolderIfPermitted(folders, setting.ResponsibleResourcesFolderID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, EDriveFolderKind.ResponsibleResources, null, string.Empty, string.Empty, canReadResponsibleFolder, canUploadResponsibleFolder, accessMode, folderKindOrNull);
        }
    }

    private async Task addTeamFoldersAsync(List<DriveFolderReference> folders, Team team, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_READ), team.ID, usesConfiguredPermissions, canReadMaterialsForTeam(userSession, team), cancellationToken);
        bool canUploadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, new PermissionKey(PermissionKeys.FILES_UPLOAD), team.ID, usesConfiguredPermissions, canUploadMaterialsForTeam(userSession, team), cancellationToken);
        addFolderIfPermitted(folders, team.MaterialsFolderID, team.DisplayName + " 자료", EDriveFolderKind.TeamMaterials, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, canUploadTeamFiles, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, team.ApprovedFolderID, team.DisplayName + " 결재 완료 자료", EDriveFolderKind.TeamApproved, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, false, accessMode, folderKindOrNull);
    }

    private void addFolderIfConfigured(List<DriveFolderReference> folders, string? folderIDOrNull, string displayName, EDriveFolderKind folderKind, int? teamID, string teamKey, string teamName, EDriveFolderUploadAccess uploadAccess, EDriveFolderKind? folderKindOrNull)
    {
        if (string.IsNullOrWhiteSpace(folderIDOrNull))
        {
            return;
        }

        if (folderKindOrNull.HasValue && folderKindOrNull.Value != folderKind)
        {
            return;
        }

        folders.Add(new DriveFolderReference(folderIDOrNull, displayName, folderKind, teamID, teamKey, teamName, uploadAccess));
    }

    private void addFolderIfPermitted(List<DriveFolderReference> folders, string? folderIDOrNull, string displayName, EDriveFolderKind folderKind, int? teamIDOrNull, string teamKey, string teamName, bool canRead, bool canUpload, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull)
    {
        if (accessMode == EDriveFolderAccessMode.Uploadable && canUpload == false)
        {
            return;
        }

        if (accessMode != EDriveFolderAccessMode.Uploadable && canRead == false && canUpload == false)
        {
            return;
        }

        EDriveFolderUploadAccess uploadAccess = canUpload ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        addFolderIfConfigured(folders, folderIDOrNull, displayName, folderKind, teamIDOrNull, teamKey, teamName, uploadAccess, folderKindOrNull);
    }
}
