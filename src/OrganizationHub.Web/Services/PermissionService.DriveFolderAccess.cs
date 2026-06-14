using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
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

        return await getMatchingFolderRootOrNullAsync(folderID, candidateFolders, cancellationToken);
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
        return await getMatchingFolderRootOrNullAsync(folderID, candidateFolders, cancellationToken);
    }

    private async Task<DriveFolderReference?> getMatchingFolderRootOrNullAsync(string folderID, IReadOnlyList<DriveFolderReference> candidateFolders, CancellationToken cancellationToken)
    {
        if (candidateFolders.Count == 0)
        {
            return null;
        }

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
}
