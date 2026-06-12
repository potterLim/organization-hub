using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveService
{
    Task<IReadOnlyList<DriveFileItem>> ListFilesAsync(string folderID, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> SearchFilesAsync(string folderID, string keyword, CancellationToken cancellationToken);
    Task<DriveFileMetadata?> GetFileOrNullAsync(string fileID, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetAncestorFolderIDsAsync(string fileID, CancellationToken cancellationToken);
    Task<DriveFileMetadata> RenameDriveItemAsync(string itemID, string name, CancellationToken cancellationToken);
    Task<DriveFileMetadata> MoveDriveItemToFolderAsync(string itemID, string targetFolderID, CancellationToken cancellationToken);
    Task<DriveFileItem> CreateFolderAsync(string parentFolderID, string folderName, CancellationToken cancellationToken);
    Task<DriveFileItem> UploadFileAsync(string targetFolderID, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<DriveFileItem> CopyFileToFolderAsync(string fileID, string targetFolderID, string fileName, CancellationToken cancellationToken);
    Task DeleteDriveItemAsync(string itemID, CancellationToken cancellationToken);
    Task<FileDownloadResult> DownloadFileAsync(string fileID, CancellationToken cancellationToken);
}
