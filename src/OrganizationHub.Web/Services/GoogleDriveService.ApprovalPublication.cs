using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using DriveDataFile = Google.Apis.Drive.v3.Data.File;

namespace OrganizationHub.Web.Services;

public sealed partial class GoogleDriveService
{
    private const string APPROVAL_OPERATION_PROPERTY = "organizationHubApproval";
    private const string PUBLICATION_FILE_FIELDS = "id,name,mimeType,parents,md5Checksum,appProperties,trashed";

    public async Task<DriveItemId> GenerateFileIdAsync(CancellationToken cancellationToken)
    {
        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        {
            FilesResource.GenerateIdsRequest request = driveService.Files.GenerateIds();
            request.Count = 1;
            Google.Apis.Drive.v3.Data.GeneratedIds ids = await request.ExecuteAsync(cancellationToken);
            return DriveItemId.CreateRequired(ids.Ids.Single());
        }
    }

    public async Task<FileDownloadResult> DownloadFileSnapshotAsync(DriveItemId fileId, CancellationToken cancellationToken)
    {
        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        {
            FilesResource.GetRequest metadataRequest = driveService.Files.Get(fileId.GetRequiredValue());
            metadataRequest.Fields = "id,name,mimeType,version,md5Checksum";
            metadataRequest.SupportsAllDrives = true;
            DriveDataFile metadata = await metadataRequest.ExecuteAsync(cancellationToken);
            MemoryStream contentStream = new MemoryStream();
            try
            {
                IDownloadProgress progress = await downloadSnapshotContentAsync(driveService, metadata, contentStream, cancellationToken);
                string fileName = metadata.Name;
                string contentType = metadata.MimeType;
                if (contentType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";
                    contentType = GoogleMimeTypes.PDF;
                }

                if (progress.Status != DownloadStatus.Completed)
                {
                    throw new IOException("Approval document download failed.", progress.Exception);
                }

                DriveDataFile currentMetadata = await metadataRequest.ExecuteAsync(cancellationToken);
                if (metadata.Version != currentMetadata.Version ||
                    (string.IsNullOrWhiteSpace(metadata.Md5Checksum) == false && string.Equals(metadata.Md5Checksum, Convert.ToHexString(MD5.HashData(contentStream.ToArray())), StringComparison.OrdinalIgnoreCase) == false))
                {
                    throw new IOException("The source document changed during capture. Submit it again.");
                }

                contentStream.Position = 0;
                return new FileDownloadResult(contentStream, DriveItemName.CreateRequired(fileName), FileContentType.CreateOptional(contentType));
            }
            catch
            {
                contentStream.Dispose();
                throw;
            }
        }
    }

    public async Task<DriveFileItem> PublishApprovalFileAsync(ApprovalFilePublishRequest publication, CancellationToken cancellationToken)
    {
        using (DriveService driveService = await createDriveServiceAsync(cancellationToken))
        using (mChangeVersionService.BeginChange(createDriveChangeKeys(publication.TargetFolderId)))
        {
            DriveDataFile? existingFileOrNull = await getPublicationFileOrNullAsync(driveService, publication.TargetFileId, cancellationToken);
            if (existingFileOrNull != null && hasPublicationReceipt(existingFileOrNull, publication.OperationKey))
            {
                DriveFileItem verifiedFile = getVerifiedPublicationFile(existingFileOrNull, publication);
                invalidatePublicationCaches(publication);
                return verifiedFile;
            }

            if (existingFileOrNull != null && publication.IsReplacement == false)
            {
                throw new InvalidOperationException("The reserved approval file ID belongs to another operation.");
            }

            if (publication.IsReplacement && (existingFileOrNull == null || existingFileOrNull.Parents == null || existingFileOrNull.Parents.Contains(publication.TargetFolderId.GetRequiredValue()) == false))
            {
                throw new InvalidOperationException("The approval replacement file is no longer in its destination folder.");
            }

            DriveDataFile metadata = new DriveDataFile();
            metadata.Name = publication.FileName.GetRequiredValue();
            metadata.AppProperties = new Dictionary<string, string> { { APPROVAL_OPERATION_PROPERTY, publication.OperationKey } };
            using (MemoryStream contentStream = new MemoryStream(publication.Content.ToArray(), false))
            {
                if (publication.IsReplacement)
                {
                    FilesResource.UpdateMediaUpload upload = driveService.Files.Update(metadata, publication.TargetFileId.GetRequiredValue(), contentStream, publication.ContentType.GetRequiredValue());
                    upload.SupportsAllDrives = true;
                    upload.Fields = PUBLICATION_FILE_FIELDS;
                    IUploadProgress progress = await upload.UploadAsync(cancellationToken);
                    ensurePublicationUploadCompleted(progress);
                }
                else
                {
                    metadata.Id = publication.TargetFileId.GetRequiredValue();
                    metadata.Parents = new List<string> { publication.TargetFolderId.GetRequiredValue() };
                    FilesResource.CreateMediaUpload upload = driveService.Files.Create(metadata, contentStream, publication.ContentType.GetRequiredValue());
                    upload.SupportsAllDrives = true;
                    upload.Fields = PUBLICATION_FILE_FIELDS;
                    IUploadProgress progress = await upload.UploadAsync(cancellationToken);
                    if (progress.Status != UploadStatus.Completed)
                    {
                        // A retry of a committed create returns 409 for the same pre-generated ID.
                        if (progress.Exception is not GoogleApiException apiException || apiException.HttpStatusCode != HttpStatusCode.Conflict)
                        {
                            ensurePublicationUploadCompleted(progress);
                        }
                    }
                }
            }

            DriveDataFile? publishedFileOrNull = await getPublicationFileOrNullAsync(driveService, publication.TargetFileId, cancellationToken);
            if (publishedFileOrNull == null)
            {
                throw new IOException("The published approval file could not be verified.");
            }

            DriveFileItem publishedFile = getVerifiedPublicationFile(publishedFileOrNull, publication);
            invalidatePublicationCaches(publication);
            return publishedFile;
        }
    }

    private static async Task<IDownloadProgress> downloadSnapshotContentAsync(DriveService driveService, DriveDataFile metadata, Stream contentStream, CancellationToken cancellationToken)
    {
        if (metadata.MimeType.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal))
        {
            FilesResource.ExportRequest exportRequest = driveService.Files.Export(metadata.Id, GoogleMimeTypes.PDF);
            return await exportRequest.DownloadAsync(contentStream, cancellationToken);
        }

        FilesResource.GetRequest downloadRequest = driveService.Files.Get(metadata.Id);
        downloadRequest.SupportsAllDrives = true;
        return await downloadRequest.DownloadAsync(contentStream, cancellationToken);
    }

    private static void ensurePublicationUploadCompleted(IUploadProgress progress)
    {
        if (progress.Status != UploadStatus.Completed)
        {
            throw new IOException("Approval file publication failed.", progress.Exception);
        }
    }

    private void invalidatePublicationCaches(ApprovalFilePublishRequest publication)
    {
        mMemoryCache.Remove(createFolderListCacheKey(publication.TargetFolderId));
        mMemoryCache.Remove(createFileMetadataCacheKey(publication.TargetFileId));
        mMemoryCache.Remove(createFileDownloadCacheKey(publication.TargetFileId));
    }

    private static async Task<DriveDataFile?> getPublicationFileOrNullAsync(DriveService driveService, DriveItemId fileId, CancellationToken cancellationToken)
    {
        try
        {
            FilesResource.GetRequest request = driveService.Files.Get(fileId.GetRequiredValue());
            request.Fields = PUBLICATION_FILE_FIELDS;
            request.SupportsAllDrives = true;
            return await request.ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static bool hasPublicationReceipt(DriveDataFile file, string operationKey)
    {
        string? storedKeyOrNull;
        return file.AppProperties != null && file.AppProperties.TryGetValue(APPROVAL_OPERATION_PROPERTY, out storedKeyOrNull) && storedKeyOrNull == operationKey;
    }

    private DriveFileItem getVerifiedPublicationFile(DriveDataFile file, ApprovalFilePublishRequest publication)
    {
        string expectedChecksum = Convert.ToHexString(MD5.HashData(publication.Content.Span));
        if (hasPublicationReceipt(file, publication.OperationKey) == false ||
            string.Equals(file.Md5Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase) == false ||
            file.Trashed == true || file.Parents == null || file.Parents.Contains(publication.TargetFolderId.GetRequiredValue()) == false)
        {
            throw new IOException("The approval file content or publication receipt does not match.");
        }

        return createDriveFileItem(file, string.Empty);
    }
}
