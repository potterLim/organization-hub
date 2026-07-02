using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class DriveBrowserService
{
    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableFoldersAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFolderReference> folders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        List<DriveFolderReference> filteredFolders = folders.OrderBy(folder => getFolderSortGroup(folder)).ThenBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
        return filteredFolders;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        SearchKeyword keyword,
        CancellationToken cancellationToken
    )
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

    public async Task<IReadOnlyList<DriveFileItem>> GetFolderFilesAsync(DriveFolderBrowseRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await GetReadableFoldersAsync(request.UserSession, request.FolderKind, cancellationToken);
        DriveFolderReference? rootFolderOrNull = folders.FirstOrDefault(folder => folder.FolderId == request.RootFolderId);
        if (rootFolderOrNull == null)
        {
            return new List<DriveFileItem>();
        }

        bool canBrowseFolder = await canBrowseFolderInsideRootAsync(rootFolderOrNull, request.FolderId, cancellationToken);
        if (canBrowseFolder == false)
        {
            return new List<DriveFileItem>();
        }

        EDriveFolderUploadAccess uploadAccess = rootFolderOrNull.CanUpload ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        DriveFolderOwnerContext ownerContext = new DriveFolderOwnerContext(rootFolderOrNull.TeamIdOrNull, rootFolderOrNull.TeamKey, rootFolderOrNull.TeamName);
        DriveFolderReferenceCreationRequest referenceRequest = new DriveFolderReferenceCreationRequest(request.FolderId, rootFolderOrNull.DisplayName, rootFolderOrNull.FolderKind, uploadAccess, ownerContext);
        DriveFolderReference currentFolder = new DriveFolderReference(referenceRequest);
        IReadOnlyList<DriveFileItem> files = await loadFolderFilesAsync(currentFolder, request.Keyword, cancellationToken);
        List<DriveFileItem> orderedFiles = orderFilesForBrowser(files);
        await mUploadAuditLogService.AttachUploadMetadataAsync(orderedFiles, cancellationToken);
        return orderedFiles;
    }

    private async Task<IReadOnlyList<DriveFileItem>> loadFolderFilesAsync(
        DriveFolderReference folder,
        SearchKeyword keyword,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFileItem> files;
        if (keyword.HasValue == false)
        {
            files = await mDriveService.ListFilesAsync(folder.FolderId, cancellationToken);
        }
        else
        {
            files = await mDriveService.SearchFilesAsync(folder.FolderId, keyword, cancellationToken);
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
        file.TeamKey = folder.TeamKey.GetOptionalValue();
        file.TeamName = folder.TeamName;
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            file.TeamName = folder.DisplayName;
        }

        file.FolderDisplayName = folder.DisplayName;
    }

    private static bool matchesKeyword(string fileName, SearchKeyword keyword)
    {
        if (keyword.HasValue == false)
        {
            return true;
        }

        return fileName.IndexOf(keyword.GetOptionalValue(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private async Task<bool> canBrowseFolderInsideRootAsync(
        DriveFolderReference rootFolder,
        DriveFolderId folderId,
        CancellationToken cancellationToken
    )
    {
        if (folderId == rootFolder.FolderId)
        {
            return true;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GoogleMimeTypes.DRIVE_FOLDER)
        {
            return false;
        }

        IReadOnlySet<DriveFolderId> ancestorFolderIds = await mDriveService.GetAncestorFolderIdsAsync(DriveItemId.CreateRequired(folderId.GetRequiredValue()), cancellationToken);
        return ancestorFolderIds.Contains(rootFolder.FolderId);
    }

    private List<DriveFileItem> orderFilesForBrowser(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> orderedFiles = files.OrderBy(file => getDriveItemSortGroup(file)).ThenBy(file => file.IsFolder ? file.Name : string.Empty).ThenByDescending(file => file.ModifiedTime).ThenBy(file => file.Name).ToList();
        return orderedFiles;
    }

    private static EDriveItemSortGroup getDriveItemSortGroup(DriveFileItem file)
    {
        if (file.IsFolder)
        {
            return EDriveItemSortGroup.Folder;
        }

        return EDriveItemSortGroup.File;
    }

    private EDriveFolderSortGroup getFolderSortGroup(DriveFolderReference folder)
    {
        if (folder.FolderKind == EDriveFolderKind.TeamApproved && folder.IsManagementTeam)
        {
            return EDriveFolderSortGroup.ManagementApproved;
        }

        return EDriveFolderSortGroup.Standard;
    }

    private enum EDriveItemSortGroup
    {
        Folder,
        File
    }

    private enum EDriveFolderSortGroup
    {
        ManagementApproved,
        Standard
    }
}
