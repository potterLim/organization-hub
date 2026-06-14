using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
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

    private IReadOnlySet<DriveFolderId>? getCachedAncestorFolderIdsOrNull(string cacheKey)
    {
        IReadOnlySet<DriveFolderId>? cachedAncestorFolderIdsOrNull = mMemoryCache.Get<IReadOnlySet<DriveFolderId>>(cacheKey);
        if (cachedAncestorFolderIdsOrNull == null)
        {
            return null;
        }

        return new HashSet<DriveFolderId>(cachedAncestorFolderIdsOrNull);
    }

    private void setAncestorFolderIdsCache(string cacheKey, IReadOnlySet<DriveFolderId> ancestorFolderIds, TimeSpan cacheDuration)
    {
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = cacheDuration;
        mMemoryCache.Set(cacheKey, new HashSet<DriveFolderId>(ancestorFolderIds), cacheEntryOptions);
    }

    private void setFileDownloadCache(DriveItemId fileId, byte[] contentBytes, string fileName, string contentType)
    {
        FileDownloadCacheEntry cacheEntry = new FileDownloadCacheEntry(contentBytes, fileName, contentType);
        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions();
        cacheEntryOptions.AbsoluteExpirationRelativeToNow = FILE_DOWNLOAD_CACHE_DURATION;
        cacheEntryOptions.Size = contentBytes.Length;
        mMemoryCache.Set(createFileDownloadCacheKey(fileId), cacheEntry, cacheEntryOptions);
    }

    private void clearDriveItemCaches(DriveItemId itemId, DriveFileMetadata? previousMetadataOrNull, DriveFileMetadata updatedMetadata)
    {
        mMemoryCache.Remove(createFileMetadataCacheKey(itemId));
        mMemoryCache.Remove(createAncestorFolderIdsCacheKey(itemId));
        mMemoryCache.Remove(createFileDownloadCacheKey(itemId));
        HashSet<DriveFolderId> parentFolderIds = new HashSet<DriveFolderId>();
        if (previousMetadataOrNull != null)
        {
            foreach (string parentFolderId in previousMetadataOrNull.ParentFolderIds)
            {
                parentFolderIds.Add(DriveFolderId.CreateOptional(parentFolderId));
            }
        }

        foreach (string parentFolderId in updatedMetadata.ParentFolderIds)
        {
            parentFolderIds.Add(DriveFolderId.CreateOptional(parentFolderId));
        }

        foreach (DriveFolderId parentFolderId in parentFolderIds)
        {
            mMemoryCache.Remove(createFolderListCacheKey(parentFolderId));
        }
    }

    private void clearDeletedDriveItemCaches(DriveItemId itemId, DriveFileMetadata previousMetadata)
    {
        mMemoryCache.Remove(createFileMetadataCacheKey(itemId));
        mMemoryCache.Remove(createAncestorFolderIdsCacheKey(itemId));
        mMemoryCache.Remove(createFileDownloadCacheKey(itemId));
        foreach (string parentFolderId in previousMetadata.ParentFolderIds)
        {
            mMemoryCache.Remove(createFolderListCacheKey(DriveFolderId.CreateOptional(parentFolderId)));
        }
    }

    private static IReadOnlyList<ChangeVersionKey> createDriveChangeKeys(DriveFolderId folderId)
    {
        List<ChangeVersionKey> changeVersionKeys = new List<ChangeVersionKey>();
        changeVersionKeys.Add(ChangeVersionKeys.DRIVE_FILES);
        changeVersionKeys.Add(ChangeVersionKeys.CreateDriveFolderKey(folderId));
        return changeVersionKeys;
    }

    private static IReadOnlyList<ChangeVersionKey> createDriveChangeKeys(DriveFileMetadata? metadataOrNull)
    {
        List<ChangeVersionKey> changeVersionKeys = new List<ChangeVersionKey>();
        changeVersionKeys.Add(ChangeVersionKeys.DRIVE_FILES);
        addMetadataFolderChangeKeys(changeVersionKeys, metadataOrNull);
        return changeVersionKeys.Distinct().ToList();
    }

    private static IReadOnlyList<ChangeVersionKey> createDriveChangeKeys(DriveFileMetadata? metadataOrNull, DriveFolderId additionalFolderId)
    {
        List<ChangeVersionKey> changeVersionKeys = new List<ChangeVersionKey>();
        changeVersionKeys.Add(ChangeVersionKeys.DRIVE_FILES);
        addMetadataFolderChangeKeys(changeVersionKeys, metadataOrNull);
        changeVersionKeys.Add(ChangeVersionKeys.CreateDriveFolderKey(additionalFolderId));
        return changeVersionKeys.Distinct().ToList();
    }

    private static void addMetadataFolderChangeKeys(List<ChangeVersionKey> changeVersionKeys, DriveFileMetadata? metadataOrNull)
    {
        if (metadataOrNull != null)
        {
            foreach (string parentFolderId in metadataOrNull.ParentFolderIds)
            {
                changeVersionKeys.Add(ChangeVersionKeys.CreateDriveFolderKey(DriveFolderId.CreateOptional(parentFolderId)));
            }
        }
    }

    private bool shouldCacheDownload(string contentType, long contentLength)
    {
        return contentType == "application/pdf" && contentLength <= MAX_CACHED_DOWNLOAD_BYTES;
    }

    private string createFolderListCacheKey(DriveFolderId folderId)
    {
        long folderVersion = mChangeVersionService.GetVersion(ChangeVersionKeys.CreateDriveFolderKey(folderId));
        return "OrganizationHub:Drive:FolderList:" + folderId.GetRequiredValue() + ":" + folderVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string createFileMetadataCacheKey(DriveItemId fileId)
    {
        return "OrganizationHub:Drive:FileMetadata:" + fileId.GetRequiredValue();
    }

    private string createAncestorFolderIdsCacheKey(DriveItemId fileId)
    {
        return "OrganizationHub:Drive:AncestorFolderIds:" + fileId.GetRequiredValue();
    }

    private string createFileDownloadCacheKey(DriveItemId fileId)
    {
        return "OrganizationHub:Drive:FileDownload:" + fileId.GetRequiredValue();
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
