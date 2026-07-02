using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private const int SHARED_FOLDER_REFERENCE_PRIORITY = 0;
    private const int TEAM_MATERIALS_FOLDER_REFERENCE_PRIORITY = 10;
    private const int TEAM_APPROVED_FOLDER_REFERENCE_PRIORITY = 20;
    private const int UNKNOWN_FOLDER_REFERENCE_PRIORITY = 100;
    private const int MANAGEMENT_APPROVAL_DESTINATION_SORT_ORDER = 0;
    private const int STANDARD_APPROVAL_DESTINATION_SORT_ORDER = 1;

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
        foreach (IGrouping<DriveFolderId, DriveFolderReference> folderGroup in folders.GroupBy(folder => folder.FolderId))
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
                return SHARED_FOLDER_REFERENCE_PRIORITY;
            case EDriveFolderKind.TeamMaterials:
                return TEAM_MATERIALS_FOLDER_REFERENCE_PRIORITY;
            case EDriveFolderKind.TeamApproved:
                return TEAM_APPROVED_FOLDER_REFERENCE_PRIORITY;
            default:
                return UNKNOWN_FOLDER_REFERENCE_PRIORITY;
        }
    }

    private int getApprovalDestinationSortOrder(DriveFolderReference folder)
    {
        if (folder.IsManagementTeam)
        {
            return MANAGEMENT_APPROVAL_DESTINATION_SORT_ORDER;
        }

        return STANDARD_APPROVAL_DESTINATION_SORT_ORDER;
    }

    private bool isManagementApprovedFolder(DriveFolderReference folder)
    {
        if (folder.FolderKind != EDriveFolderKind.TeamApproved)
        {
            return false;
        }

        return folder.IsManagementTeam;
    }
}
