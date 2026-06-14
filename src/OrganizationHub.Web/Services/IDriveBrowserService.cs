using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveBrowserService
{
    Task<IReadOnlyList<DriveFolderReference>> GetReadableFoldersAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DriveFileItem>> GetFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        SearchKeyword keyword,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DriveFileItem>> GetFolderFilesAsync(DriveFolderBrowseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DriveFileItem>> GetRecentUploadedFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken);

    void InvalidateRecentFiles();

    void InvalidateRecentFiles(DriveFolderId folderId);

    void SuppressRecentUploadMerge(DriveItemId fileId);
}
