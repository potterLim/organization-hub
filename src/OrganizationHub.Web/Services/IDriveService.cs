using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveService
{
    Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(DriveFolderId folderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(DriveFolderId folderId, SearchKeyword keyword, CancellationToken cancellationToken);
    Task<DriveFileMetadata?> GetFileOrNullAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<IReadOnlySet<DriveFolderId>> GetAncestorFolderIdsAsync(DriveItemId fileId, CancellationToken cancellationToken);
    Task<DriveFileMetadata> RenameDriveItemAsync(DriveItemId itemId, string name, CancellationToken cancellationToken);
    Task<DriveFileMetadata> MoveDriveItemToFolderAsync(DriveItemId itemId, DriveFolderId targetFolderId, CancellationToken cancellationToken);
    Task<DriveFileItem> CreateFolderAsync(DriveFolderId parentFolderId, string folderName, CancellationToken cancellationToken);
    Task<DriveFileItem> UploadFileAsync(DriveFolderId targetFolderId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<DriveFileItem> CopyFileToFolderAsync(DriveItemId fileId, DriveFolderId targetFolderId, string fileName, CancellationToken cancellationToken);
    Task DeleteDriveItemAsync(DriveItemId itemId, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadFileAsync(DriveItemId fileId, CancellationToken cancellationToken);
}
