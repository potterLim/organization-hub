using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    public async Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(string folderID, CancellationToken cancellationToken)
    {
        string cacheKey = createFolderListCacheKey(folderID);
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = getCachedDriveFileItemsOrNull(cacheKey);
        if (cachedFilesOrNull != null)
        {
            return cachedFilesOrNull;
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.ListRequest request = driveService.Files.List();
        request.Q = "'" + escapeDriveQueryValue(folderID) + "' in parents and trashed = false";
        request.Fields = "files(id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size)";
        request.OrderBy = "modifiedTime desc";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;
        DriveFileList fileList = await request.ExecuteAsync(cancellationToken);
        IReadOnlyList<DriveFileItem> files = createDriveFileItems(fileList, string.Empty);
        setDriveFileItemsCache(cacheKey, files, FOLDER_LIST_CACHE_DURATION);
        return cloneDriveFileItems(files);
    }

    public async Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(string folderID, string keyword, CancellationToken cancellationToken)
    {
        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.ListRequest request = driveService.Files.List();
        request.Q = "'" + escapeDriveQueryValue(folderID) + "' in parents and trashed = false and name contains '" + escapeDriveQueryValue(keyword) + "'";
        request.Fields = "files(id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size)";
        request.OrderBy = "modifiedTime desc";
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;
        DriveFileList fileList = await request.ExecuteAsync(cancellationToken);
        return createDriveFileItems(fileList, string.Empty);
    }

    public async Task<DriveFileMetadata?> GetFileOrNullAsync(string fileID, CancellationToken cancellationToken)
    {
        string cacheKey = createFileMetadataCacheKey(fileID);
        DriveFileMetadata? cachedMetadataOrNull = getCachedDriveFileMetadataOrNull(cacheKey);
        if (cachedMetadataOrNull != null)
        {
            return cachedMetadataOrNull;
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.GetRequest request = driveService.Files.Get(fileID);
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

    public async Task<IReadOnlySet<string>> GetAncestorFolderIDsAsync(string fileID, CancellationToken cancellationToken)
    {
        string cacheKey = createAncestorFolderIDsCacheKey(fileID);
        IReadOnlySet<string>? cachedAncestorFolderIDsOrNull = getCachedAncestorFolderIDsOrNull(cacheKey);
        if (cachedAncestorFolderIDsOrNull != null)
        {
            return new HashSet<string>(cachedAncestorFolderIDsOrNull);
        }

        HashSet<string> ancestorFolderIDs = new HashSet<string>();
        Queue<string> pendingFolderIDs = new Queue<string>();
        DriveFileMetadata? fileMetadataOrNull = await GetFileOrNullAsync(fileID, cancellationToken);
        if (fileMetadataOrNull == null)
        {
            return ancestorFolderIDs;
        }

        foreach (string parentFolderID in fileMetadataOrNull.ParentFolderIDs)
        {
            if (ancestorFolderIDs.Add(parentFolderID))
            {
                pendingFolderIDs.Enqueue(parentFolderID);
            }
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        int inspectedFolderCount = 0;
        while (pendingFolderIDs.Count > 0 && inspectedFolderCount < 40)
        {
            inspectedFolderCount = inspectedFolderCount + 1;
            string currentFolderID = pendingFolderIDs.Dequeue();
            FilesResource.GetRequest request = driveService.Files.Get(currentFolderID);
            request.Fields = "id,parents";
            request.SupportsAllDrives = true;
            DriveDataFile folder = await request.ExecuteAsync(cancellationToken);
            if (folder.Parents == null)
            {
                continue;
            }

            foreach (string parentFolderID in folder.Parents)
            {
                if (ancestorFolderIDs.Add(parentFolderID))
                {
                    pendingFolderIDs.Enqueue(parentFolderID);
                }
            }
        }

        setAncestorFolderIDsCache(cacheKey, ancestorFolderIDs, ANCESTOR_CACHE_DURATION);
        return ancestorFolderIDs;
    }
}
