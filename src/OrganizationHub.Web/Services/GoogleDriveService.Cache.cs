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
        keys.Add(ChangeVersionKeys.CreateDriveFolderKey(new DriveFolderID(folderID)));
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
                keys.Add(ChangeVersionKeys.CreateDriveFolderKey(new DriveFolderID(parentFolderID)));
            }
        }

        if (string.IsNullOrWhiteSpace(additionalFolderID) == false)
        {
            keys.Add(ChangeVersionKeys.CreateDriveFolderKey(new DriveFolderID(additionalFolderID)));
        }

        return keys.Distinct().ToList();
    }

    private bool shouldCacheDownload(string contentType, long contentLength)
    {
        return contentType == "application/pdf" && contentLength <= MAX_CACHED_DOWNLOAD_BYTES;
    }

    private string createFolderListCacheKey(string folderID)
    {
        long folderVersion = mChangeVersionService.GetVersion(ChangeVersionKeys.CreateDriveFolderKey(new DriveFolderID(folderID)));
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
