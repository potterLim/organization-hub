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
    public async Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, DriveItemName itemName, CancellationToken cancellationToken)
    {
        string requiredItemId = itemId.GetRequiredValue();
        string requiredItemName = itemName.GetRequiredValue();
        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(itemId, cancellationToken);
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Name = requiredItemName;
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
        DriveFileUploadRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.ConflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            return await replaceFileContentAsync(request.ReplacementFileId, request.ContentStream, request.FileName, request.ContentType, cancellationToken);
        }

        DriveFileNameConflictCheckResult conflictCheckResult = await CheckFileNameConflictAsync(request.TargetFolderId, request.FileName, cancellationToken);
        DriveItemName uploadFileName = request.FileName;
        if (request.ConflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            uploadFileName = conflictCheckResult.SuggestedNewFileName;
        }

        return await createFileAsync(request.TargetFolderId, request.ContentStream, uploadFileName, request.ContentType, cancellationToken);
    }

    private async Task<DriveFileItem> createFileAsync(
        DriveFolderId targetFolderId,
        Stream fileStream,
        DriveItemName fileName,
        FileContentType contentType,
        CancellationToken cancellationToken
    )
    {
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        string requiredFileName = fileName.GetRequiredValue();
        string requiredContentType = contentType.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = requiredFileName;
            metadata.Parents = new List<string> { requiredTargetFolderId };
            FilesResource.CreateMediaUpload upload = driveService.Files.Create(metadata, fileStream, requiredContentType);
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

    public async Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, DriveItemName folderName, CancellationToken cancellationToken)
    {
        string requiredParentFolderId = parentFolderId.GetRequiredValue();
        string requiredFolderName = folderName.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(parentFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = requiredFolderName;
            metadata.MimeType = GoogleMimeTypes.DRIVE_FOLDER;
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
        DriveFileCopyRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.ConflictResolution == EDriveFileNameConflictResolution.ReplaceExistingFile)
        {
            FileDownloadResult downloadResult = await DownloadFileAsync(request.SourceFileId, cancellationToken);
            using (Stream contentStream = downloadResult.ContentStream)
            {
                return await replaceFileContentAsync(
                    request.ReplacementFileId,
                    contentStream,
                    request.FileName,
                    FileContentType.CreateOptional(downloadResult.ContentType),
                    cancellationToken);
            }
        }

        DriveFileNameConflictCheckResult conflictCheckResult = await CheckFileNameConflictAsync(request.TargetFolderId, request.FileName, cancellationToken);
        DriveItemName copiedFileName = request.FileName;
        if (request.ConflictResolution == EDriveFileNameConflictResolution.AddNewFile && conflictCheckResult.HasConflict)
        {
            copiedFileName = conflictCheckResult.SuggestedNewFileName;
        }

        return await copyFileToFolderAsync(request.SourceFileId, request.TargetFolderId, copiedFileName, cancellationToken);
    }

    private async Task<DriveFileItem> copyFileToFolderAsync(
        DriveItemId fileId,
        DriveFolderId targetFolderId,
        DriveItemName fileName,
        CancellationToken cancellationToken
    )
    {
        string requiredFileId = fileId.GetRequiredValue();
        string requiredTargetFolderId = targetFolderId.GetRequiredValue();
        string requiredFileName = fileName.GetRequiredValue();
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(targetFolderId)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = requiredFileName;
            metadata.Parents = new List<string> { requiredTargetFolderId };
            FilesResource.CopyRequest request = driveService.Files.Copy(metadata, requiredFileId);
            request.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size";
            request.SupportsAllDrives = true;
            DriveDataFile copiedFile = await request.ExecuteAsync(cancellationToken);
            mMemoryCache.Remove(createFolderListCacheKey(targetFolderId));
            return createDriveFileItem(copiedFile, string.Empty);
        }
    }

    private async Task<DriveFileItem> replaceFileContentAsync(
        DriveItemId fileId,
        Stream contentStream,
        DriveItemName fileName,
        FileContentType contentType,
        CancellationToken cancellationToken
    )
    {
        string requiredItemId = fileId.GetRequiredValue();
        string requiredFileName = fileName.GetRequiredValue();
        string requiredContentType = contentType.GetRequiredValue();
        DriveFileMetadata? previousMetadataOrNull = await GetFileOrNullAsync(fileId, cancellationToken);
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(previousMetadataOrNull)))
        {
            DriveService driveService = await createDriveServiceAsync(cancellationToken);
            DriveDataFile updateMetadata = new DriveDataFile();
            updateMetadata.Name = requiredFileName;
            FilesResource.UpdateMediaUpload upload = driveService.Files.Update(updateMetadata, requiredItemId, contentStream, requiredContentType);
            upload.Fields = "id,name,mimeType,createdTime,modifiedTime,iconLink,thumbnailLink,size,parents";
            upload.SupportsAllDrives = true;
            IUploadProgress uploadProgress = await upload.UploadAsync(cancellationToken);
            if (uploadProgress.Status == UploadStatus.Failed)
            {
                throw new InvalidOperationException("Google Drive file replacement failed.", uploadProgress.Exception);
            }

            DriveDataFile replacedFile = upload.ResponseBody;
            DriveFileMetadata updatedMetadata = createDriveFileMetadata(replacedFile);
            clearDriveItemCaches(fileId, previousMetadataOrNull, updatedMetadata);
            return createDriveFileItem(replacedFile, string.Empty);
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
        DriveItemName downloadedFileName = DriveItemName.CreateRequired(metadata.Name);
        FileContentType contentType = FileContentType.CreateOptional(metadata.MimeType);

        if (shouldCacheDownload(contentType, memoryStream.Length))
        {
            setFileDownloadCache(fileId, memoryStream.ToArray(), downloadedFileName, contentType);
            memoryStream.Position = 0;
        }

        return new FileDownloadResult(memoryStream, downloadedFileName, contentType);
    }
}
