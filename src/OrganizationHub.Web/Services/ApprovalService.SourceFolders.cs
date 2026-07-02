using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private List<DriveFolderReference> createApprovalSourceFolders(IReadOnlyList<DriveFolderReference> readableFolders)
    {
        IEnumerable<DriveFolderReference> sourceFolders = readableFolders.Where(folder => folder.FolderKind == EDriveFolderKind.TeamMaterials);
        return sourceFolders.OrderBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
    }
}
