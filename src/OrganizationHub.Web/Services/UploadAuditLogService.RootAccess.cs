using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    private async Task<DriveFolderReference?> getReadableRootOrNullAsync(UserSession userSession, DriveFolderReference rootFolder, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> readableFolders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, rootFolder.FolderKind, cancellationToken);
        foreach (DriveFolderReference readableFolder in readableFolders)
        {
            if (isSameRoot(readableFolder, rootFolder))
            {
                return readableFolder;
            }
        }

        return null;
    }

    private static bool isSameRoot(DriveFolderReference left, DriveFolderReference right)
    {
        if (left.FolderID != right.FolderID)
        {
            return false;
        }

        if (left.FolderKind != right.FolderKind)
        {
            return false;
        }

        return left.TeamID == right.TeamID;
    }

    private async Task<bool> isUploadInsideRootAsync(DriveItemID itemID, DriveFolderID rootFolderID, CancellationToken cancellationToken)
    {
        if (itemID.Value == rootFolderID.Value)
        {
            return true;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(itemID.Value, cancellationToken);
        return ancestorFolderIDs.Contains(rootFolderID.Value);
    }

    private async Task<DriveFolderReference?> getMatchingReadableRootOrNullAsync(UploadAuditLog auditLog, IReadOnlyList<DriveFolderReference> readableRoots, CancellationToken cancellationToken)
    {
        foreach (DriveFolderReference readableRoot in readableRoots)
        {
            if (isAuditLogCandidateForRoot(auditLog, readableRoot) == false)
            {
                continue;
            }

            bool isFolderInsideRoot = await isUploadInsideRootAsync(new DriveItemID(auditLog.FolderID), new DriveFolderID(readableRoot.FolderID), cancellationToken);
            if (isFolderInsideRoot)
            {
                return readableRoot;
            }
        }

        return null;
    }

    private static bool isAuditLogCandidateForRoot(UploadAuditLog auditLog, DriveFolderReference readableRoot)
    {
        if (auditLog.FolderKind != readableRoot.FolderKind)
        {
            return false;
        }

        if (readableRoot.TeamID.HasValue)
        {
            return auditLog.TeamID.HasValue && auditLog.TeamID.Value == readableRoot.TeamID.Value;
        }

        return auditLog.TeamID.HasValue == false;
    }
}
