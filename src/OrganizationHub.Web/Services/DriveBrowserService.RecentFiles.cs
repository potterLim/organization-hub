using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class DriveBrowserService
{
    public async Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken
    )
    {
        return await GetRecentFilesAsync(userSession, folderKind, maximumCount, EManagementTeamFilter.IncludeManagementTeam, cancellationToken);
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken
    )
    {
        string cacheKey = createRecentFilesCacheKey(userSession, folderKind, maximumCount, managementTeamFilter);
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = mMemoryCache.Get<IReadOnlyList<DriveFileItem>>(cacheKey);
        if (cachedFilesOrNull != null)
        {
            return cloneDriveFileItems(cachedFilesOrNull);
        }

        IReadOnlyList<DriveFileItem> files = await GetFilesAsync(userSession, folderKind, SearchKeyword.Empty, cancellationToken);
        IEnumerable<DriveFileItem> recentFileQuery = files.Where(file => file.IsFolder == false);
        if (managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam)
        {
            recentFileQuery = recentFileQuery.Where(file => isManagementFile(file) == false);
        }

        List<DriveFileItem> recentFiles = recentFileQuery.Take(maximumCount.GetRequiredValue()).ToList();
        mMemoryCache.Set(cacheKey, cloneDriveFileItems(recentFiles), RECENT_FILES_CACHE_DURATION);
        return recentFiles;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetRecentUploadedFilesAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken
    )
    {
        string cacheKey = createRecentUploadedFilesCacheKey(userSession, folderKind, maximumCount, managementTeamFilter);
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = mMemoryCache.Get<IReadOnlyList<DriveFileItem>>(cacheKey);
        if (cachedFilesOrNull != null)
        {
            return cloneDriveFileItems(cachedFilesOrNull);
        }

        RecentItemLimit snapshotCount = RecentUploadedFileCachePolicy.CreateSnapshotLimit(maximumCount);
        IReadOnlyList<UploadAuditLogItem> recentUploads = await mUploadAuditLogService.GetRecentUploadSnapshotsForReadableRootsAsync(userSession, folderKind, snapshotCount, managementTeamFilter, cancellationToken);
        List<DriveFileItem> files = RecentUploadedFileItemMapper.CreateDriveFileItems(recentUploads, maximumCount);

        mMemoryCache.Set(cacheKey, cloneDriveFileItems(files), RecentUploadedFileCachePolicy.CACHE_DURATION);
        RecentUploadedFileCacheRefreshRequest refreshRequest = new RecentUploadedFileCacheRefreshRequest
        {
            CacheKey = cacheKey,
            SnapshotUploads = recentUploads,
            UserSession = userSession,
            FolderKind = folderKind,
            MaximumCount = maximumCount,
            ManagementTeamFilter = managementTeamFilter
        };
        mRecentUploadedFileCacheRefreshQueue.TryEnqueue(refreshRequest);
        return files;
    }

    private async Task mergeRecentUploadedFilesAsync(
        DriveFolderReference folder,
        SearchKeyword keyword,
        List<DriveFileItem> files,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<UploadAuditLogItem> recentUploads = await mUploadAuditLogService.GetRecentUploadsForFolderAsync(folder.FolderId, RECENT_UPLOAD_MERGE_WINDOW, cancellationToken);
        if (recentUploads.Count == 0)
        {
            return;
        }

        HashSet<DriveItemId> existingFileIds = new HashSet<DriveItemId>(files.Select(file => DriveItemId.CreateOptional(file.Id)));
        foreach (UploadAuditLogItem recentUpload in recentUploads)
        {
            DriveItemId recentUploadFileId = DriveItemId.CreateOptional(recentUpload.FileId);
            if (existingFileIds.Contains(recentUploadFileId) || isRecentUploadMergeSuppressed(recentUploadFileId) || matchesKeyword(recentUpload.FileName, keyword) == false)
            {
                continue;
            }

            DriveFileItem file = RecentUploadedFileItemMapper.CreateDriveFileItem(recentUpload, folder);
            files.Add(file);
            existingFileIds.Add(DriveItemId.CreateOptional(file.Id));
        }
    }

    private static bool isManagementFile(DriveFileItem file)
    {
        return new OrganizationTeamKey(file.TeamKey).IsManagementTeamKey;
    }
}
