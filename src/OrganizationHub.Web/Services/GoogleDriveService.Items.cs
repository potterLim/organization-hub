using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
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
}
