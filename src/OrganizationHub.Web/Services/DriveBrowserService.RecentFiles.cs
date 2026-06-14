using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class DriveBrowserService
{
    public async Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, CancellationToken cancellationToken)
    {
        return await GetRecentFilesAsync(userSession, folderKind, maximumCount, EManagementTeamFilter.IncludeManagementTeam, cancellationToken);
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetRecentFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
    {
        string cacheKey = createRecentFilesCacheKey(userSession, folderKind, maximumCount, managementTeamFilter);
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = mMemoryCache.Get<IReadOnlyList<DriveFileItem>>(cacheKey);
        if (cachedFilesOrNull != null)
        {
            return cloneDriveFileItems(cachedFilesOrNull);
        }

        IReadOnlyList<DriveFileItem> files = await GetFilesAsync(userSession, folderKind, string.Empty, cancellationToken);
        IEnumerable<DriveFileItem> recentFileQuery = files.Where(file => file.IsFolder == false);
        if (managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam)
        {
            recentFileQuery = recentFileQuery.Where(file => isManagementFile(file) == false);
        }

        List<DriveFileItem> recentFiles = recentFileQuery.Take(maximumCount).ToList();
        mMemoryCache.Set(cacheKey, cloneDriveFileItems(recentFiles), RECENT_FILES_CACHE_DURATION);
        return recentFiles;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetRecentUploadedFilesAsync(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter, CancellationToken cancellationToken)
    {
        string cacheKey = createRecentUploadedFilesCacheKey(userSession, folderKind, maximumCount, managementTeamFilter);
        IReadOnlyList<DriveFileItem>? cachedFilesOrNull = mMemoryCache.Get<IReadOnlyList<DriveFileItem>>(cacheKey);
        if (cachedFilesOrNull != null)
        {
            return cloneDriveFileItems(cachedFilesOrNull);
        }

        IReadOnlyList<UploadAuditLogItem> recentUploads = await mUploadAuditLogService.GetRecentUploadsForReadableRootsAsync(userSession, folderKind, maximumCount, managementTeamFilter, cancellationToken);
        List<DriveFileItem> files = new List<DriveFileItem>();
        foreach (UploadAuditLogItem recentUpload in recentUploads)
        {
            files.Add(createDriveFileItemFromRecentUpload(recentUpload));
        }

        mMemoryCache.Set(cacheKey, cloneDriveFileItems(files), RECENT_FILES_CACHE_DURATION);
        return files;
    }

    private async Task mergeRecentUploadedFilesAsync(DriveFolderReference folder, string keyword, List<DriveFileItem> files, CancellationToken cancellationToken)
    {
        IReadOnlyList<UploadAuditLogItem> recentUploads = await mUploadAuditLogService.GetRecentUploadsForFolderAsync(folder.FolderID, RECENT_UPLOAD_MERGE_WINDOW, cancellationToken);
        if (recentUploads.Count == 0)
        {
            return;
        }

        HashSet<string> existingFileIDs = new HashSet<string>(files.Select(file => file.ID));
        foreach (UploadAuditLogItem recentUpload in recentUploads)
        {
            if (existingFileIDs.Contains(recentUpload.FileID) || isRecentUploadMergeSuppressed(recentUpload.FileID) || matchesKeyword(recentUpload.FileName, keyword) == false)
            {
                continue;
            }

            DriveFileItem file = createDriveFileItemFromRecentUpload(recentUpload, folder);
            files.Add(file);
            existingFileIDs.Add(file.ID);
        }
    }

    private static DriveFileItem createDriveFileItemFromRecentUpload(UploadAuditLogItem recentUpload, DriveFolderReference folder)
    {
        DriveFileItem file = createDriveFileItemFromRecentUpload(recentUpload);
        file.TeamKey = folder.TeamKey;
        file.TeamName = string.IsNullOrWhiteSpace(recentUpload.TeamName) ? folder.TeamName : recentUpload.TeamName;
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            file.TeamName = folder.DisplayName;
        }

        file.FolderDisplayName = folder.DisplayName;
        return file;
    }

    private static DriveFileItem createDriveFileItemFromRecentUpload(UploadAuditLogItem recentUpload)
    {
        DriveFileItem file = new DriveFileItem();
        file.ID = recentUpload.FileID;
        file.Name = recentUpload.FileName;
        file.MimeType = inferMimeTypeFromFileName(recentUpload.FileName);
        file.CreatedTime = recentUpload.UploadedAt;
        file.ModifiedTime = recentUpload.UploadedAt;
        file.IsFolder = false;
        file.TeamName = recentUpload.TeamName;
        file.FolderDisplayName = recentUpload.FolderDisplayName;
        file.UploadedByDisplayName = recentUpload.UploaderDisplayName;
        file.UploadedAt = recentUpload.UploadedAt;
        file.CanPreviewInSite = file.MimeType == "application/pdf";
        return file;
    }

    private static string inferMimeTypeFromFileName(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        return string.Empty;
    }

    private static bool isManagementFile(DriveFileItem file)
    {
        return string.Equals(file.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }
}
