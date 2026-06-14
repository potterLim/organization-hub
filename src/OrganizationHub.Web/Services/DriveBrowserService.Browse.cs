using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class DriveBrowserService
{
    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        List<DriveFolderReference> filteredFolders = folders.OrderBy(folder => getFolderSortOrder(folder)).ThenBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
        return filteredFolders;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        List<Task<IReadOnlyList<DriveFileItem>>> fileTasks = new List<Task<IReadOnlyList<DriveFileItem>>>();
        foreach (DriveFolderReference folder in folders)
        {
            fileTasks.Add(loadFolderFilesAsync(folder, keyword, cancellationToken));
        }

        IReadOnlyList<DriveFileItem>[] fileGroups = await Task.WhenAll(fileTasks);
        List<DriveFileItem> mergedFiles = new List<DriveFileItem>();
        foreach (IReadOnlyList<DriveFileItem> fileGroup in fileGroups)
        {
            mergedFiles.AddRange(fileGroup);
        }

        List<DriveFileItem> orderedFiles = mergedFiles.OrderByDescending(file => file.ModifiedTime).ThenBy(file => file.Name).ToList();
        await mUploadAuditLogService.AttachUploadMetadataAsync(orderedFiles, cancellationToken);
        return orderedFiles;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetFolderFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string rootFolderID, string folderID, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await GetReadableFoldersAsync(userSession, folderKind, cancellationToken);
        DriveFolderReference? rootFolderOrNull = folders.FirstOrDefault(folder => folder.FolderID == rootFolderID);
        if (rootFolderOrNull == null)
        {
            return new List<DriveFileItem>();
        }

        bool canBrowseFolder = await canBrowseFolderInsideRootAsync(rootFolderOrNull, folderID, cancellationToken);
        if (canBrowseFolder == false)
        {
            return new List<DriveFileItem>();
        }

        EDriveFolderUploadAccess uploadAccess = rootFolderOrNull.CanUpload ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        IReadOnlyList<DriveFileItem> files = await loadFolderFilesAsync(new DriveFolderReference(folderID, rootFolderOrNull.DisplayName, rootFolderOrNull.FolderKind, rootFolderOrNull.TeamID, rootFolderOrNull.TeamKey, rootFolderOrNull.TeamName, uploadAccess), keyword, cancellationToken);
        List<DriveFileItem> orderedFiles = orderFilesForBrowser(files);
        await mUploadAuditLogService.AttachUploadMetadataAsync(orderedFiles, cancellationToken);
        return orderedFiles;
    }

    private async Task<IReadOnlyList<DriveFileItem>> loadFolderFilesAsync(DriveFolderReference folder, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            files = await mDriveService.ListFilesAsync(folder.FolderID, cancellationToken);
        }
        else
        {
            files = await mDriveService.SearchFilesAsync(folder.FolderID, keyword, cancellationToken);
        }

        List<DriveFileItem> mergedFiles = files.ToList();
        foreach (DriveFileItem file in mergedFiles)
        {
            applyFolderContext(file, folder);
        }

        await mergeRecentUploadedFilesAsync(folder, keyword, mergedFiles, cancellationToken);
        return mergedFiles;
    }

    private static void applyFolderContext(DriveFileItem file, DriveFolderReference folder)
    {
        file.TeamKey = folder.TeamKey;
        file.TeamName = folder.TeamName;
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            file.TeamName = folder.DisplayName;
        }

        file.FolderDisplayName = folder.DisplayName;
    }

    private static bool matchesKeyword(string fileName, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return fileName.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private async Task<bool> canBrowseFolderInsideRootAsync(DriveFolderReference rootFolder, string folderID, CancellationToken cancellationToken)
    {
        if (folderID == rootFolder.FolderID)
        {
            return true;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return false;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        return ancestorFolderIDs.Contains(rootFolder.FolderID);
    }

    private List<DriveFileItem> orderFilesForBrowser(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> orderedFiles = files.OrderBy(file => file.IsFolder ? 0 : 1).ThenBy(file => file.IsFolder ? file.Name : string.Empty).ThenByDescending(file => file.ModifiedTime).ThenBy(file => file.Name).ToList();
        return orderedFiles;
    }

    private int getFolderSortOrder(DriveFolderReference folder)
    {
        if (folder.FolderKind != EDriveFolderKind.TeamApproved)
        {
            return 1;
        }

        if (folder.IsManagementTeam)
        {
            return 0;
        }

        return 1;
    }
}
