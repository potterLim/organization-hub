using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveService
{
    Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(DriveFolderId folderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(DriveFolderId folderId, SearchKeyword keyword, CancellationToken cancellationToken);
    Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(DriveFolderId folderId, DriveItemName fileName, CancellationToken cancellationToken);
    Task<DriveFileMetadata?> GetFileOrNullAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<IReadOnlySet<DriveFolderId>> GetAncestorFolderIdsAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, DriveItemName itemName, CancellationToken cancellationToken);
    Task<DriveFileMetadata> MoveDriveItemToFolderAsync(DriveItemId itemId, DriveFolderId targetFolderId, CancellationToken cancellationToken);
    Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, DriveItemName folderName, CancellationToken cancellationToken);
    Task<DriveFileItem> UploadFileAsync(DriveFileUploadRequest request, CancellationToken cancellationToken);
    Task<DriveFileItem?> GetUploadReceiptOrNullAsync(DriveItemId fileId, string operationKey, CancellationToken cancellationToken);
    Task<DriveFileItem> CopyFileToFolderAsync(DriveFileCopyRequest request, CancellationToken cancellationToken);
    Task DeleteDriveItemAsync(DriveItemId itemId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadFileAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadFileSnapshotAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadFileSnapshotAsync(DriveItemId fileId, FileByteLimit maximumBytes, CancellationToken cancellationToken);
    Task<DriveItemId> GenerateFileIdAsync(CancellationToken cancellationToken);
    Task<DriveFileItem> PublishApprovalFileAsync(ApprovalFilePublishRequest request, CancellationToken cancellationToken);
}
