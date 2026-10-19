using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    public async Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(DriveFolderId folderId, CancellationToken cancellationToken)
    {
        string requiredFolderId = folderId.GetRequiredValue();
        string cacheKey = createFolderListCacheKey(folderId);
        IReadOnlyList<DriveFileItem> cachedFiles;
        if (tryGetCachedDriveFileItems(cacheKey, out cachedFiles))
        {
            return cachedFiles;
        }

        IReadOnlyList<DriveFileItem> files = await listFilesFromDriveAsync(folderId, cancellationToken);
        setDriveFileItemsCache(cacheKey, files, FOLDER_LIST_CACHE_DURATION);
        return cloneDriveFileItems(files);
    }

    public async Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(
        DriveFolderId folderId,
        DriveItemName fileName,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<DriveFileItem> existingItems = await listFilesFromDriveAsync(folderId, cancellationToken);
        List<DriveFileItem> conflictingFiles = new List<DriveFileItem>();
        string requestedFileName = fileName.GetRequiredValue();
        foreach (DriveFileItem item in existingItems)
        {
            if (item.IsFolder)
            {
                continue;
            }

            if (string.Equals(item.Name, requestedFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                conflictingFiles.Add(item);
            }
        }

        DriveItemName suggestedNewFileName = DriveFileNamePolicy.CreateAvailableFileName(fileName, existingItems);
        return new DriveFileNameConflictCheckResult(fileName, suggestedNewFileName, conflictingFiles);
    }

    public async Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(
        DriveFolderId folderId,
        SearchKeyword keyword,
        CancellationToken cancellationToken
    )
    {
        string requiredFolderId = folderId.GetRequiredValue();
        string requiredKeyword = keyword.GetOptionalValue();
        string query = "'" + escapeDriveQueryValue(requiredFolderId) + "' in parents and trashed = false and name contains '" + escapeDriveQueryValue(requiredKeyword) + "'";
        return await listFolderPagesAsync(query, cancellationToken);
    }


    public async Task<DriveFileMetadata?> GetFileOrNullAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        string requiredFileId = fileId.GetRequiredValue();
        string cacheKey = createFileMetadataCacheKey(fileId);
        DriveFileMetadata? cachedMetadataOrNull = getCachedDriveFileMetadataOrNull(cacheKey);
        if (cachedMetadataOrNull != null)
        {
            return cachedMetadataOrNull;
        }

        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        {
            FilesResource.GetRequest request = driveService.Files.Get(requiredFileId);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            try
            {
                DriveDataFile driveFile = await request.ExecuteAsync(cancellationToken);
                DriveFileMetadata metadata = createDriveFileMetadata(driveFile);
                setDriveFileMetadataCache(cacheKey, metadata, FILE_METADATA_CACHE_DURATION);
                return cloneDriveFileMetadata(metadata);
            }
            catch (Google.GoogleApiException exception)
            {
                if (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw;
            }
        }
    }

    public async Task<IReadOnlySet<DriveFolderId>> GetAncestorFolderIdsAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        string cacheKey = createAncestorFolderIdsCacheKey(fileId);
        IReadOnlySet<DriveFolderId> cachedAncestorFolderIds;
        if (tryGetCachedAncestorFolderIds(cacheKey, out cachedAncestorFolderIds))
        {
            return new HashSet<DriveFolderId>(cachedAncestorFolderIds);
        }

        HashSet<DriveFolderId> ancestorFolderIds = new HashSet<DriveFolderId>();
        Queue<string> pendingFolderIds = new Queue<string>();
        DriveFileMetadata? fileMetadataOrNull = await GetFileOrNullAsync(fileId, cancellationToken);
        if (fileMetadataOrNull == null)
        {
            return ancestorFolderIds;
        }

        foreach (string parentFolderId in fileMetadataOrNull.ParentFolderIds)
        {
            if (ancestorFolderIds.Add(DriveFolderId.CreateOptional(parentFolderId)))
            {
                pendingFolderIds.Enqueue(parentFolderId);
            }
        }

        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        {
            int inspectedFolderCount = 0;
            while (pendingFolderIds.Count > 0 && inspectedFolderCount < MAX_ANCESTOR_FOLDER_INSPECTION_COUNT)
            {
                inspectedFolderCount = inspectedFolderCount + 1;
                string currentFolderId = pendingFolderIds.Dequeue();
                FilesResource.GetRequest request = driveService.Files.Get(currentFolderId);
                request.Fields = "id,parents";
                request.SupportsAllDrives = true;
                DriveDataFile folder = await request.ExecuteAsync(cancellationToken);
                if (folder.Parents == null)
                {
                    continue;
                }

                foreach (string parentFolderId in folder.Parents)
                {
                    if (ancestorFolderIds.Add(DriveFolderId.CreateOptional(parentFolderId)))
                    {
                        pendingFolderIds.Enqueue(parentFolderId);
                    }
                }
            }

            setAncestorFolderIdsCache(cacheKey, ancestorFolderIds, ANCESTOR_CACHE_DURATION);
            return ancestorFolderIds;
        }
    }
    private Task<IReadOnlyList<DriveFileItem>> listFilesFromDriveAsync(DriveFolderId folderId, CancellationToken cancellationToken)
    {
        string query = "'" + escapeDriveQueryValue(folderId.GetRequiredValue()) + "' in parents and trashed = false";
        return listFolderPagesAsync(query, cancellationToken);
    }

    private async Task<IReadOnlyList<DriveFileItem>> listFolderPagesAsync(string query, CancellationToken cancellationToken)
    {
        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        {
            FilesResource.ListRequest request = driveService.Files.List();
            request.Q = query;
            request.Fields = "nextPageToken,files(id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size)";
            request.OrderBy = "modifiedTime desc";
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;
            List<DriveFileItem> items = new List<DriveFileItem>();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                DriveFileList page = await request.ExecuteAsync(cancellationToken);
                items.AddRange(createDriveFileItems(page, string.Empty));
                request.PageToken = page.NextPageToken;
            }
            while (string.IsNullOrEmpty(request.PageToken) == false);
            return items;
        }
    }

}
