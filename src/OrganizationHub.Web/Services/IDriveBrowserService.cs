using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveBrowserService
{
    Task<IReadOnlyList<DriveFolderReference>> GetReadableFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> GetFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string keyword, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> GetFolderFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string rootFolderID, string folderID, string keyword, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriveFileItem>> GetRecentUploadedFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken);
    void InvalidateRecentFiles();
    void InvalidateRecentFiles(string folderID);
    void SuppressRecentUploadMerge(string fileID);
}
