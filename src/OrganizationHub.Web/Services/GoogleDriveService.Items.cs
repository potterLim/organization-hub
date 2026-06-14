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
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    public async Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Google Drive item name is required.", nameof(name));
        }

        string requiredItemId = itemId.GetRequiredValue();
        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemId, cancellationToken);
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Name = name.Trim();
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, requiredItemId);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            DriveDataFile updatedFile = await request.ExecuteAsync(cancellationToken);
            DriveFileMetadata updatedMetadata = createDriveFileMetadata(updatedFile);
            clearDriveItemCaches(itemId, previousMetadataOrNull, updatedMetadata);
            return cloneDriveFileMetadata(updatedMetadata);
        }
    }

    public async Task<DriveFileMetadata> MoveDriveItemToFolderAsync(
        DriveItemId itemId,
        DriveFolderId targetFolderId,
        CancellationToken cancellationToken
    )
    {
        string requiredItemId = itemId.GetRequiredValue();
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemId, cancellationToken);
        if (previousMetadataOrNull == null)
        {
            throw new InvalidOperationException("Google Drive item was not found.");
        }

        if (previousMetadataOrNull.ParentFolderIds.Contains(requiredTargetFolderId))
        {
            return previousMetadataOrNull;
        }

        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull, targetFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, requiredItemId);
            request.AddParents = requiredTargetFolderId;
            request.RemoveParents = string.Join(",", previousMetadataOrNull.ParentFolderIds);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            DriveDataFile updatedFile = await request.ExecuteAsync(cancellationToken);
            DriveFileMetadata updatedMetadata = createDriveFileMetadata(updatedFile);
            clearDriveItemCaches(itemId, previousMetadataOrNull, updatedMetadata);
            return cloneDriveFileMetadata(updatedMetadata);
        }
    }

    public async Task<DriveFileItem> UploadFileAsync(
        DriveFolderId targetFolderId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken
    )
    {
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = fileName;
            metadata.Parents = new List<string> { requiredTargetFolderId };
            FilesResource.CreateMediaUpload upload = driveService.Files.Create(metadata, fileStream, contentType);
            upload.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            upload.SupportsAllDrives = true;
            IUploadProgress uploadProgress = await upload.UploadAsync(cancellationToken);
            if (uploadProgress.Status == UploadStatus.Failed)
            {
                throw new InvalidOperationException("Google Drive upload failed.", uploadProgress.Exception);
            }

            DriveDataFile uploadedFile = upload.ResponseBody;
            mMemoryCache.Remove(createFolderListCacheKey(targetFolderId));
            return createDriveFileItem(uploadedFile, string.Empty);
        }
    }

    public async Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, string folderName, CancellationToken cancellationToken)
    {
        string requiredParentFolderId = parentFolderId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(parentFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = folderName.Trim();
            metadata.MimeType = "application/vnd.google-apps.folder";
            metadata.Parents = new List<string> { requiredParentFolderId };
            FilesResource.CreateRequest request = driveService.Files.Create(metadata);
            request.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            request.SupportsAllDrives = true;
            DriveDataFile createdFolder = await request.ExecuteAsync(cancellationToken);
            mMemoryCache.Remove(createFolderListCacheKey(parentFolderId));
            return createDriveFileItem(createdFolder, string.Empty);
        }
    }

    public async Task<DriveFileItem> CopyFileToFolderAsync(
        DriveItemId fileId,
        DriveFolderId targetFolderId,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        string requiredFileId = fileId.GetRequiredValue();
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = fileName;
            metadata.Parents = new List<string> { requiredTargetFolderId };
            FilesResource.CopyRequest request = driveService.Files.Copy(metadata, requiredFileId);
            request.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            request.SupportsAllDrives = true;
            DriveDataFile copiedFile = await request.ExecuteAsync(cancellationToken);
            mMemoryCache.Remove(createFolderListCacheKey(targetFolderId));
            return createDriveFileItem(copiedFile, string.Empty);
        }
    }

    public async Task DeleteDriveItemAsync(DriveItemId itemId, CancellationToken cancellationToken)
    {
        string requiredItemId = itemId.GetRequiredValue();
        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemId, cancellationToken);
        if (previousMetadataOrNull == null)
        {
            return;
        }

        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Trashed = true;
            FilesResource.UpdateRequest request = driveService.Files.Update(updateMetadata, requiredItemId);
            request.Fields = "id,name,mimeType,parents";
            request.SupportsAllDrives = true;
            await request.ExecuteAsync(cancellationToken);
            clearDeletedDriveItemCaches(itemId, previousMetadataOrNull);
        }
    }

    public async Task<FileDownloadResult> DownloadFileAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        string requiredFileId = fileId.GetRequiredValue();
        FileDownloadCacheEntry? cachedDownloadOrNull = mMemoryCache.Get<FileDownloadCacheEntry>(createFileDownloadCacheKey(fileId));
        if (cachedDownloadOrNull != null)
        {
            return cachedDownloadOrNull.CreateDownloadResult();
        }

        DriveService driveService = await createDriveServiceAsync(cancellationToken);
        FilesResource.GetRequest metadataRequest = driveService.Files.Get(requiredFileId);
        metadataRequest.Fields = "id,name,mimeType";
        metadataRequest.SupportsAllDrives = true;
        DriveDataFile metadata = await metadataRequest.ExecuteAsync(cancellationToken);
        FilesResource.GetRequest downloadRequest = driveService.Files.Get(requiredFileId);
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
            setFileDownloadCache(fileId, memoryStream.ToArray(), metadata.Name, contentType);
            memoryStream.Position = 0;
        }

        return new FileDownloadResult(memoryStream, metadata.Name, contentType);
    }
}
