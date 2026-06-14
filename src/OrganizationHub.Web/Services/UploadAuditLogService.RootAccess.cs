using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    private async Task<DriveFolderReference?> getReadableRootOrNullAsync(
        UserSession userSession,
        DriveFolderReference rootFolder,
        CancellationToken cancellationToken
    )
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
        if (left.FolderId != right.FolderId)
        {
            return false;
        }

        if (left.FolderKind != right.FolderKind)
        {
            return false;
        }

        return left.TeamIdOrNull == right.TeamIdOrNull;
    }

    private async Task<bool> isUploadInsideRootAsync(DriveItemId itemId, DriveFolderId rootFolderId, CancellationToken cancellationToken)
    {
        if (itemId.GetRequiredValue() == rootFolderId.GetRequiredValue())
        {
            return true;
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(itemId, cancellationToken);
        return ancestorFolderIds.Contains(rootFolderId);
    }

    private async Task<DriveFolderReference?> getMatchingReadableRootOrNullAsync(
        UploadAuditLog auditLog,
        IReadOnlyList<DriveFolderReference> readableRoots,
        CancellationToken cancellationToken
    )
    {
        foreach (DriveFolderReference readableRoot in readableRoots)
        {
            if (isAuditLogCandidateForRoot(auditLog, readableRoot) == false)
            {
                continue;
            }

            bool isFolderInsideRoot = await isUploadInsideRootAsync(DriveItemId.CreateOptional(auditLog.FolderId), readableRoot.FolderId, cancellationToken);
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

        int? readableRootTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(readableRoot.TeamIdOrNull);
        if (readableRootTeamIdValueOrNull.HasValue)
        {
            return auditLog.TeamId == readableRootTeamIdValueOrNull;
        }

        return auditLog.TeamId.HasValue == false;
    }
}
