using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<DriveFolderReference?> getAllowedFolderRootForFolderAsync(
        UserSession userSession,
        DriveFolderId folderId,
        EDriveFolderRootSelection rootSelection,
        CancellationToken cancellationToken
    )
    {
        if (folderId.HasValue == false)
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GoogleMimeTypes.DRIVE_FOLDER)
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

        return await getMatchingFolderRootOrNullAsync(folderId, candidateFolders, cancellationToken);
    }

    private async Task<DriveFolderReference?> getReadableFolderRootForFolderAsync(
        UserSession userSession,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        if (folderId.HasValue == false)
        {
            return null;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GoogleMimeTypes.DRIVE_FOLDER)
        {
            return null;
        }

        IReadOnlyList<DriveFolderReference> candidateFolders = await getDriveFoldersAsync(userSession, EDriveFolderAccessMode.Readable, null, cancellationToken);
        return await getMatchingFolderRootOrNullAsync(folderId, candidateFolders, cancellationToken);
    }

    private async Task<DriveFolderReference?> getMatchingFolderRootOrNullAsync(
        DriveFolderId folderId,
        IReadOnlyList<DriveFolderReference> candidateFolders,
        CancellationToken cancellationToken
    )
    {
        if (candidateFolders.Count == 0)
        {
            return null;
        }

        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (candidateFolder.FolderId == folderId)
            {
                bool canUseRootFolder = await canUseFolderInsideOfficialRootAsync(folderId, cancellationToken);
                return canUseRootFolder ? candidateFolder : null;
            }
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        foreach (DriveFolderReference candidateFolder in candidateFolders)
        {
            if (ancestorFolderIds.Contains(candidateFolder.FolderId) == false)
            {
                continue;
            }

            bool canUseFolder = await canUseFolderInsideOfficialRootAsync(folderId, cancellationToken);
            if (canUseFolder)
            {
                return candidateFolder;
            }
        }

        return null;
    }
}
