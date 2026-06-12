using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;
using DriveFileList = Google.Apis.Drive.v3.Data.FileList;

namespace OrganizationHub.Web.Services;

public sealed class GoogleDriveService : IDriveService
{
    private const int MAX_CACHED_DOWNLOAD_BYTES = 20971520;

    private static readonly string[] DRIVE_SCOPES = new[] { DriveService.Scope.Drive };
    private static readonly TimeSpan FOLDER_LIST_CACHE_DURATION = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan FILE_METADATA_CACHE_DURATION = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ANCESTOR_CACHE_DURATION = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FILE_DOWNLOAD_CACHE_DURATION = TimeSpan.FromMinutes(2);

    private readonly IGoogleApiCredentialFactory mCredentialFactory;
    private readonly GoogleApiOptions mOptions;
    private readonly IMemoryCache mMemoryCache;
    private readonly IChangeVersionService mChangeVersionService;

    public GoogleDriveService(IGoogleApiCredentialFactory credentialFactory, IOptions<GoogleApiOptions> options, IMemoryCache memoryCache, IChangeVersionService changeVersionService)
    {
        mCredentialFactory = credentialFactory;
        mOptions = options.Value;
        mMemoryCache = memoryCache;
        mChangeVersionService = changeVersionService;
    }

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

    public async Task<DriveFileMetadata> RenameDriveItemAsync(string itemID, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemID))
        {
            throw new ArgumentException("Google Drive item ID is required.", nameof(itemID));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Google Drive item name is required.", nameof(name));
        }

        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemID, cancellationToken);
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Name = name.Trim();
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, itemID);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            DriveDataFile updatedFile = await request.ExecuteAsync(cancellationToken);
            DriveFileMetadata updatedMetadata = createDriveFileMetadata(updatedFile);
            clearDriveItemCaches(itemID, previousMetadataOrNull, updatedMetadata);
            return cloneDriveFileMetadata(updatedMetadata);
        }
    }

    public async Task<DriveFileMetadata> MoveDriveItemToFolderAsync(string itemID, string targetFolderID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemID))
        {
            throw new ArgumentException("Google Drive item ID is required.", nameof(itemID));
        }

        if (string.IsNullOrWhiteSpace(targetFolderID))
        {
            throw new ArgumentException("Google Drive target folder ID is required.", nameof(targetFolderID));
        }

        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemID, cancellationToken);
        if (previousMetadataOrNull == null)
        {
            throw new InvalidOperationException("Google Drive item was not found.");
        }

        if (previousMetadataOrNull.ParentFolderIDs.Contains(targetFolderID))
        {
            return previousMetadataOrNull;
        }

        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull, targetFolderID)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, itemID);
            request.AddParents = targetFolderID.Trim();
            request.RemoveParents = string.Join(",", previousMetadataOrNull.ParentFolderIDs);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            DriveDataFile updatedFile = await request.ExecuteAsync(cancellationToken);
            DriveFileMetadata updatedMetadata = createDriveFileMetadata(updatedFile);
            clearDriveItemCaches(itemID, previousMetadataOrNull, updatedMetadata);
            return cloneDriveFileMetadata(updatedMetadata);
        }
    }

    public async Task<DriveFileItem> UploadFileAsync(string targetFolderID, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderID)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = fileName;
            metadata.Parents = new List<string> { targetFolderID };
            FilesResource.CreateMediaUpload upload = driveService.Files.Create(metadata, fileStream, contentType);
            upload.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            upload.SupportsAllDrives = true;
            IUploadProgress uploadProgress = await upload.UploadAsync(cancellationToken);
            if (uploadProgress.Status == UploadStatus.Failed)
            {
                throw new InvalidOperationException("Google Drive upload failed.", uploadProgress.Exception);
            }

            DriveDataFile uploadedFile = upload.ResponseBody;
            mMemoryCache.Remove(createFolderListCacheKey(targetFolderID));
            return createDriveFileItem(uploadedFile, string.Empty);
        }
    }

    public async Task<DriveFileItem> CreateFolderAsync(string parentFolderID, string folderName, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(parentFolderID)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = folderName.Trim();
            metadata.MimeType = "application/vnd.google-apps.folder";
            metadata.Parents = new List<string> { parentFolderID };
            FilesResource.CreateRequest request = driveService.Files.Create(metadata);
            request.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            request.SupportsAllDrives = true;
            DriveDataFile createdFolder = await request.ExecuteAsync(cancellationToken);
            mMemoryCache.Remove(createFolderListCacheKey(parentFolderID));
            return createDriveFileItem(createdFolder, string.Empty);
        }
    }

    public async Task<DriveFileItem> CopyFileToFolderAsync(string fileID, string targetFolderID, string fileName, CancellationToken cancellationToken)
    {
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderID)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = fileName;
            metadata.Parents = new List<string> { targetFolderID };
            FilesResource.CopyRequest request = driveService.Files.Copy(metadata, fileID);
            request.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            request.SupportsAllDrives = true;
            DriveDataFile copiedFile = await request.ExecuteAsync(cancellationToken);
            mMemoryCache.Remove(createFolderListCacheKey(targetFolderID));
            return createDriveFileItem(copiedFile, string.Empty);
        }
    }

    public async Task DeleteDriveItemAsync(string itemID, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemID))
        {
            throw new ArgumentException("Google Drive item ID is required.", nameof(itemID));
        }

        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemID, cancellationToken);
        if (previousMetadataOrNull == null)
        {
            return;
        }

        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Trashed = true;
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, itemID);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            await request.ExecuteAsync(cancellationToken);
            clearDeletedDriveItemCaches(itemID, previousMetadataOrNull);
        }
    }

    public async Task<FileDownloadResult> DownloadFileAsync(string fileID, CancellationToken cancellationToken)
    {
        FileDownloadCacheEntry? cachedDownloadOrNull = mMemoryCache.Get<FileDownloadCacheEntry>(createFileDownloadCacheKey(fileID));
        if (cachedDownloadOrNull != null)
        {
            return cachedDownloadOrNull.CreateDownloadResult();
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.GetRequest metadataRequest = driveService.Files.Get(fileID);
        metadataRequest.Fields = "id,name,mimeType";
        metadataRequest.SupportsAllDrives = true;
        DriveDataFile metadata = await metadataRequest.ExecuteAsync(cancellationToken);
        FilesResource.GetRequest downloadRequest = driveService.Files.Get(fileID);
        downloadRequest.SupportsAllDrives = true;
        MemoryStream memoryStream = new MemoryStream();
        IDownloadProgress downloadProgress = await downloadRequest.DownloadAsync(memoryStream, cancellationToken);
        if (downloadProgress.Status == DownloadStatus.Failed)
        {
            throw new InvalidOperationException("Google Drive download failed.", downloadProgress.Exception);
        }

        memoryStream.Position = 0;
        string contentType = metadata.MimeType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = "application/octet-stream";
        }

        if (shouldCacheDownload(contentType, memoryStream.Length))
        {
            setFileDownloadCache(fileID, memoryStream.ToArray(), metadata.Name, contentType);
            memoryStream.Position = 0;
        }

        return new FileDownloadResult(memoryStream, metadata.Name, contentType);
    }

    private async Task<DriveService> createDriveServiceAsync(CancellationToken cancellationToken)
    {
        IConfigurableHttpClientInitializer credential = await mCredentialFactory.CreateHttpClientInitializerAsync(DRIVE_SCOPES, cancellationToken);
        BaseClientService.Initializer initializer = new BaseClientService.Initializer();
        initializer.HttpClientInitializer = credential;
        initializer.ApplicationName = mOptions.ApplicationName;
        return new DriveService(initializer);
    }

    private IReadOnlyList<DriveFileItem>? getCachedDriveFileItemsOrNull(string cacheKey)
    {
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = mMemoryCache.Get<IReadOnlyList<DriveFileItem>>(cacheKey);
        if (cachedFilesOrNull == null)
        {
            return null;
        }

        return cloneDriveFileItems(cachedFilesOrNull);
    }

    private void setDriveFileItemsCache(string cacheKey, IReadOnlyList<DriveFileItem> files, TimeSpan cacheDuration)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = cacheDuration;
        mMemoryCache.Set(cacheKey, cloneDriveFileItems(files), cacheEntryOptions);
    }

    private DriveFileMetadata? getCachedDriveFileMetadataOrNull(string cacheKey)
    {
        DriveFileMetadata? cachedMetadataOrNull = mMemoryCache.Get<DriveFileMetadata>(cacheKey);
        if (cachedMetadataOrNull == null)
        {
            return null;
        }

        return cloneDriveFileMetadata(cachedMetadataOrNull);
    }

    private void setDriveFileMetadataCache(string cacheKey, DriveFileMetadata metadata, TimeSpan cacheDuration)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = cacheDuration;
        mMemoryCache.Set(cacheKey, cloneDriveFileMetadata(metadata), cacheEntryOptions);
    }

    private IReadOnlySet<string>? getCachedAncestorFolderIDsOrNull(string cacheKey)
    {
        IReadOnlySet<string>? cachedAncestorFolderIDsOrNull = mMemoryCache.Get<IReadOnlySet<string>>(cacheKey);
        if (cachedAncestorFolderIDsOrNull == null)
        {
            return null;
        }

        return new HashSet<string>(cachedAncestorFolderIDsOrNull);
    }

    private void setAncestorFolderIDsCache(string cacheKey, IReadOnlySet<string> ancestorFolderIDs, TimeSpan cacheDuration)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = cacheDuration;
        mMemoryCache.Set(cacheKey, new HashSet<string>(ancestorFolderIDs), cacheEntryOptions);
    }

    private void setFileDownloadCache(string fileID, byte[] contentBytes, string fileName, string contentType)
    {
        FileDownloadCacheEntry cacheEntry = new FileDownloadCacheEntry(contentBytes, fileName, contentType);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = FILE_DOWNLOAD_CACHE_DURATION;
        cacheEntryOptions.Size = contentBytes.Length;
        mMemoryCache.Set(createFileDownloadCacheKey(fileID), cacheEntry, cacheEntryOptions);
    }

    private void clearDriveItemCaches(string itemID, DriveFileMetadata? previousMetadataOrNull, DriveFileMetadata updatedMetadata)
    {
        mMemoryCache.Remove(createFileMetadataCacheKey(itemID));
        mMemoryCache.Remove(createAncestorFolderIDsCacheKey(itemID));
        mMemoryCache.Remove(createFileDownloadCacheKey(itemID));
        HashSet<string> parentFolderIDs = new HashSet<string>();
        if (previousMetadataOrNull != null)
        {
            foreach (string parentFolderID in previousMetadataOrNull.ParentFolderIDs)
            {
                parentFolderIDs.Add(parentFolderID);
            }
        }

        foreach (string parentFolderID in updatedMetadata.ParentFolderIDs)
        {
            parentFolderIDs.Add(parentFolderID);
        }

        foreach (string parentFolderID in parentFolderIDs)
        {
            mMemoryCache.Remove(createFolderListCacheKey(parentFolderID));
        }
    }

    private void clearDeletedDriveItemCaches(string itemID, DriveFileMetadata previousMetadata)
    {
        mMemoryCache.Remove(createFileMetadataCacheKey(itemID));
        mMemoryCache.Remove(createAncestorFolderIDsCacheKey(itemID));
        mMemoryCache.Remove(createFileDownloadCacheKey(itemID));
        foreach (string parentFolderID in previousMetadata.ParentFolderIDs)
        {
            mMemoryCache.Remove(createFolderListCacheKey(parentFolderID));
        }
    }

    private static IReadOnlyList<string> createDriveChangeKeys(string folderID)
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.DRIVE_FILES);
        keys.Add(ChangeVersionKeys.CreateDriveFolderKey(folderID));
        return keys;
    }

    private static IReadOnlyList<string> createDriveChangeKeys(DriveFileMetadata? metadataOrNull, string additionalFolderID = "")
    {
        List<string> keys = new List<string>();
        keys.Add(ChangeVersionKeys.DRIVE_FILES);
        if (metadataOrNull != null)
        {
            foreach (string parentFolderID in metadataOrNull.ParentFolderIDs)
            {
                keys.Add(ChangeVersionKeys.CreateDriveFolderKey(parentFolderID));
            }
        }

        if (string.IsNullOrWhiteSpace(additionalFolderID) == false)
        {
            keys.Add(ChangeVersionKeys.CreateDriveFolderKey(additionalFolderID));
        }

        return keys.Distinct().ToList();
    }

    private bool shouldCacheDownload(string contentType, long contentLength)
    {
        return contentType == "application/pdf" && contentLength <= MAX_CACHED_DOWNLOAD_BYTES;
    }

    private string createFolderListCacheKey(string folderID)
    {
        long folderVersion = mChangeVersionService.GetVersion(ChangeVersionKeys.CreateDriveFolderKey(folderID));
        return "OrganizationHub:Drive:FolderList:" + folderID + ":" + folderVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string createFileMetadataCacheKey(string fileID)
    {
        return "OrganizationHub:Drive:FileMetadata:" + fileID;
    }

    private string createAncestorFolderIDsCacheKey(string fileID)
    {
        return "OrganizationHub:Drive:AncestorFolderIDs:" + fileID;
    }

    private string createFileDownloadCacheKey(string fileID)
    {
        return "OrganizationHub:Drive:FileDownload:" + fileID;
    }

    private IReadOnlyList<DriveFileItem> createDriveFileItems(DriveFileList fileList, string teamName)
    {
        List<DriveFileItem> items = new List<DriveFileItem>();
        if (fileList.Files == null)
        {
            return items;
        }

        foreach (DriveDataFile file in fileList.Files)
        {
            DriveFileItem item = createDriveFileItem(file, teamName);
            items.Add(item);
        }

        return items;
    }

    private IReadOnlyList<DriveFileItem> cloneDriveFileItems(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> clonedFiles = new List<DriveFileItem>();
        foreach (DriveFileItem file in files)
        {
            clonedFiles.Add(cloneDriveFileItem(file));
        }

        return clonedFiles;
    }

    private DriveFileItem cloneDriveFileItem(DriveFileItem file)
    {
        DriveFileItem clonedFile = new DriveFileItem();
        clonedFile.ID = file.ID;
        clonedFile.Name = file.Name;
        clonedFile.MimeType = file.MimeType;
        clonedFile.CreatedTime = file.CreatedTime;
        clonedFile.ModifiedTime = file.ModifiedTime;
        clonedFile.IconLink = file.IconLink;
        clonedFile.ThumbnailLink = file.ThumbnailLink;
        clonedFile.IsFolder = file.IsFolder;
        clonedFile.TeamName = file.TeamName;
        clonedFile.FolderDisplayName = file.FolderDisplayName;
        clonedFile.UploadedByDisplayName = file.UploadedByDisplayName;
        clonedFile.UploadedAt = file.UploadedAt;
        clonedFile.CanPreviewInSite = file.CanPreviewInSite;
        clonedFile.Size = file.Size;
        return clonedFile;
    }

    private DriveFileItem createDriveFileItem(DriveDataFile file, string teamName)
    {
        DriveFileItem item = new DriveFileItem();
        item.ID = getNullAsEmpty(file.Id);
        item.Name = getNullAsEmpty(file.Name);
        item.MimeType = getNullAsEmpty(file.MimeType);
        item.CreatedTime = file.CreatedTimeDateTimeOffset.HasValue ? file.CreatedTimeDateTimeOffset.Value.DateTime : null;
        item.ModifiedTime = file.ModifiedTimeDateTimeOffset.HasValue ? file.ModifiedTimeDateTimeOffset.Value.DateTime : null;
        item.IconLink = getNullAsEmpty(file.IconLink);
        item.ThumbnailLink = getNullAsEmpty(file.ThumbnailLink);
        item.IsFolder = file.MimeType == "application/vnd.google-apps.folder";
        item.TeamName = teamName;
        item.CanPreviewInSite = file.MimeType == "application/pdf";
        item.Size = file.Size;
        return item;
    }

    private DriveFileMetadata createDriveFileMetadata(DriveDataFile file)
    {
        DriveFileMetadata metadata = new DriveFileMetadata();
        metadata.ID = getNullAsEmpty(file.Id);
        metadata.Name = getNullAsEmpty(file.Name);
        metadata.MimeType = getNullAsEmpty(file.MimeType);
        if (file.Parents == null)
        {
            metadata.ParentFolderIDs = new List<string>();
        }
        else
        {
            metadata.ParentFolderIDs = new List<string>(file.Parents);
        }

        return metadata;
    }

    private DriveFileMetadata cloneDriveFileMetadata(DriveFileMetadata metadata)
    {
        DriveFileMetadata clonedMetadata = new DriveFileMetadata();
        clonedMetadata.ID = metadata.ID;
        clonedMetadata.Name = metadata.Name;
        clonedMetadata.MimeType = metadata.MimeType;
        clonedMetadata.ParentFolderIDs = new List<string>(metadata.ParentFolderIDs);
        return clonedMetadata;
    }

    private string escapeDriveQueryValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private string getNullAsEmpty(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return string.Empty;
        }

        return valueOrNull;
    }

    private sealed class FileDownloadCacheEntry
    {
        public byte[] ContentBytes
        {
            get; private init;
        }
        public string FileName
        {
            get; private init;
        }
        public string ContentType
        {
            get; private init;
        }

        public FileDownloadCacheEntry(byte[] contentBytes, string fileName, string contentType)
        {
            ContentBytes = contentBytes;
            FileName = fileName;
            ContentType = contentType;
        }

        public FileDownloadResult CreateDownloadResult()
        {
            MemoryStream memoryStream = new MemoryStream(ContentBytes, false);
            return new FileDownloadResult(memoryStream, FileName, ContentType);
        }
    }
}
