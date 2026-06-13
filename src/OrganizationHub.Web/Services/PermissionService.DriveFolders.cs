using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<IReadOnlyList<DriveFolderReference>> getDriveFoldersAsync(UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, CancellationToken cancellationToken)
    {
        List<DriveFolderReference> folders = new List<DriveFolderReference>();
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            GlobalSetting? settingOrNull = await dbContext.GlobalSettings.OrderBy(setting => setting.ID).FirstOrDefaultAsync(cancellationToken);
            if (settingOrNull != null)
            {
                await addGlobalFoldersAsync(folders, settingOrNull, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }

            IQueryable<Team> teamQuery = dbContext.Teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder);
            if (usesConfiguredPermissions == false && userSession.IsManagementRole() == false)
            {
                if (userSession.TeamID.HasValue == false)
                {
                    IReadOnlyList<DriveFolderReference> filteredTeamlessFolders = filterFolders(folders, accessMode);
                    IReadOnlyList<DriveFolderReference> allowedTeamlessFolders = await filterFoldersInsideOfficialRootAsync(filteredTeamlessFolders, cancellationToken);
                    return deduplicateFolderReferences(allowedTeamlessFolders);
                }

                int teamID = userSession.TeamID.Value;
                teamQuery = teamQuery.Where(team => team.ID == teamID);
            }

            List<Team> teams = await teamQuery.ToListAsync(cancellationToken);
            foreach (Team team in teams)
            {
                await addTeamFoldersAsync(folders, team, userSession, accessMode, folderKindOrNull, usesConfiguredPermissions, cancellationToken);
            }
        }

        IReadOnlyList<DriveFolderReference> filteredFolders = filterFolders(folders, accessMode);
        IReadOnlyList<DriveFolderReference> allowedFolders = await filterFoldersInsideOfficialRootAsync(filteredFolders, cancellationToken);
        return deduplicateFolderReferences(allowedFolders);
    }

    private async Task addGlobalFoldersAsync(List<DriveFolderReference> folders, GlobalSetting setting, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadGlobalFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, null, usesConfiguredPermissions, true, cancellationToken);
        bool canUploadPublic = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadPublicMaterials(userSession.Role), cancellationToken);
        bool canUploadCommon = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadCommonMaterials(userSession.Role), cancellationToken);
        bool canUploadWorkResources = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadWorkResources(userSession.Role), cancellationToken);
        addFolderIfPermitted(folders, setting.PublicFolderID, OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME, EDriveFolderKind.Public, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadPublic, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.CommonFolderID, OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME, EDriveFolderKind.Common, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadCommon, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, setting.WorkResourcesFolderID, OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME, EDriveFolderKind.WorkResources, null, string.Empty, string.Empty, canReadGlobalFiles, canUploadWorkResources, accessMode, folderKindOrNull);
        bool canReadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, null, usesConfiguredPermissions, userSession.IsTeamLeaderOrHigherRole(), cancellationToken);
        if (canReadResponsibleFolder)
        {
            bool canUploadResponsibleFolder = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, null, usesConfiguredPermissions, UserPermissionPolicy.CanUploadResponsibleResources(userSession.Role), cancellationToken);
            addFolderIfPermitted(folders, setting.ResponsibleResourcesFolderID, OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME, EDriveFolderKind.ResponsibleResources, null, string.Empty, string.Empty, canReadResponsibleFolder, canUploadResponsibleFolder, accessMode, folderKindOrNull);
        }
    }

    private async Task addTeamFoldersAsync(List<DriveFolderReference> folders, Team team, UserSession userSession, EDriveFolderAccessMode accessMode, EDriveFolderKind? folderKindOrNull, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        bool canReadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_READ, team.ID, usesConfiguredPermissions, canReadMaterialsForTeam(userSession, team), cancellationToken);
        bool canUploadTeamFiles = await canUseConfiguredOrFallbackPermissionAsync(userSession, PermissionKeys.FILES_UPLOAD, team.ID, usesConfiguredPermissions, canUploadMaterialsForTeam(userSession, team), cancellationToken);
        addFolderIfPermitted(folders, team.MaterialsFolderID, team.DisplayName + " 자료", EDriveFolderKind.TeamMaterials, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, canUploadTeamFiles, accessMode, folderKindOrNull);
        addFolderIfPermitted(folders, team.ApprovedFolderID, team.DisplayName + " 결재 완료 자료", EDriveFolderKind.TeamApproved, team.ID, team.TeamKey, team.DisplayName, canReadTeamFiles, false, accessMode, folderKindOrNull);
    }

    private async Task<DriveFolderReference?> getAllowedFolderRootForFolderAsync(UserSession userSession, string folderID, EDriveFolderRootSelection rootSelection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderID))
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return null;
        }

        IReadOnlyList<DriveFolderReference> candidateFolders;
        if (rootSelection == EDriveFolderRootSelection.ApprovalDestination)
        {
            candidateFolders = await GetApprovalDestinationDriveFoldersAsync(userSession, cancellationToken);
        }
        else
        {
            EDriveFolderAccessMode accessMode = rootSelection == EDriveFolderRootSelection.Uploadable ? EDriveFolderAccessMode.Uploadable : EDriveFolderAccessMode.Readable;
            candidateFolders = await getDriveFoldersAsync(userSession, accessMode, null, cancellationToken);
        }

        if (candidateFolders.Count == 0)
        {
            return null;
        }

        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (candidateFolder.FolderID == folderID)
            {
                bool canUseRootFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
                if (canUseRootFolder)
                {
                    return candidateFolder;
                }

                return null;
            }
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (ancestorFolderIDs.Contains(candidateFolder.FolderID) == false)
            {
                continue;
            }

            bool canUseFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
            if (canUseFolder)
            {
                return candidateFolder;
            }
        }

        return null;
    }

    private async Task<DriveFolderReference?> getReadableFolderRootForFolderAsync(UserSession userSession, string folderID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderID))
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return null;
        }

        IReadOnlyList<DriveFolderReference> candidateFolders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, null, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (candidateFolder.FolderID == folderID)
            {
                bool canUseRootFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
                return canUseRootFolder ? candidateFolder : null;
            }
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (ancestorFolderIDs.Contains(candidateFolder.FolderID) == false)
            {
                continue;
            }

            bool canUseFolder = await canUseFolderInsideOfficialRootAsync(folderID, cancellationToken);
            if (canUseFolder)
            {
                return candidateFolder;
            }
        }

        return null;
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

    private IReadOnlyList<DriveFolderReference> filterFolders(List<DriveFolderReference> folders, EDriveFolderAccessMode accessMode)
    {
        if (accessMode != EDriveFolderAccessMode.Uploadable)
        {
            return folders;
        }

        List<DriveFolderReference> uploadableFolders = folders.Where(folder => folder.CanUpload).ToList();
        return uploadableFolders;
    }

    private static IReadOnlyList<DriveFolderReference> deduplicateFolderReferences(IReadOnlyList<DriveFolderReference> folders)
    {
        List<DriveFolderReference> deduplicatedFolders = new List<DriveFolderReference>();
        foreach (IGrouping<string, DriveFolderReference> folderGroup in folders.GroupBy(folder => folder.FolderID))
        {
            DriveFolderReference selectedFolder = folderGroup.OrderBy(getFolderReferencePriority).ThenByDescending(folder => folder.CanUpload).ThenBy(folder => folder.DisplayName).First();
            deduplicatedFolders.Add(selectedFolder);
        }

        return deduplicatedFolders;
    }

    private static int getFolderReferencePriority(DriveFolderReference folder)
    {
        switch (folder.FolderKind)
        {
            case EDriveFolderKind.Public:
            case EDriveFolderKind.Common:
            case EDriveFolderKind.WorkResources:
            case EDriveFolderKind.ResponsibleResources:
                return 0;
            case EDriveFolderKind.TeamMaterials:
                return 10;
            case EDriveFolderKind.TeamApproved:
                return 20;
            default:
                return 100;
        }
    }

    private int getApprovalDestinationSortOrder(DriveFolderReference folder)
    {
        if (folder.IsManagementTeam)
        {
            return 0;
        }

        return 1;
    }

    private bool isManagementApprovedFolder(DriveFolderReference folder)
    {
        if (folder.FolderKind != EDriveFolderKind.TeamApproved)
        {
            return false;
        }

        return folder.IsManagementTeam;
    }

    private bool canUploadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (isManagementTeam(team))
        {
            return UserPermissionPolicy.CanUploadManagementMaterials(userSession.Role);
        }

        return UserPermissionPolicy.CanUploadTeamMaterials(userSession.Role);
    }

    private bool canReadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (userSession.IsManagementRole())
        {
            return true;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool canWriteTeamWorkFallback(UserSession userSession, Team team)
    {
        if (UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role))
        {
            return true;
        }

        if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
        {
            return false;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool isManagementTeam(Team team)
    {
        return string.Equals(team.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }
}
