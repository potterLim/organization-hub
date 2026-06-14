using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
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
}
