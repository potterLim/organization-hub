using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class DriveBrowserService : IDriveBrowserService
{
    private const string GOOGLE_DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
    private const string RECENT_FILES_CACHE_VERSION_KEY = "OrganizationHub:DriveBrowser:RecentFiles:Version";
    private const string RECENT_UPLOAD_SUPPRESSION_PREFIX = "OrganizationHub:DriveBrowser:RecentUploadSuppressed:";
    private static readonly TimeSpan RECENT_FILES_CACHE_DURATION = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RECENT_FILES_CACHE_VERSION_DURATION = TimeSpan.FromHours(8);
    private static readonly TimeSpan RECENT_UPLOAD_MERGE_WINDOW = TimeSpan.FromMinutes(5);

    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly IUploadAuditLogService mUploadAuditLogService;
    private readonly IMemoryCache mMemoryCache;
    private readonly IChangeVersionService mChangeVersionService;

    public DriveBrowserService(IPermissionService permissionService, IDriveService driveService, IUploadAuditLogService uploadAuditLogService, IMemoryCache memoryCache, IChangeVersionService changeVersionService)
    {
        mPermissionService = permissionService;
        mDriveService = driveService;
        mUploadAuditLogService = uploadAuditLogService;
        mMemoryCache = memoryCache;
        mChangeVersionService = changeVersionService;
    }

    public async Task<IReadOnlyList<DriveFolderReference>> GetReadableFoldersAsync(UserSession userSession, EDriveFolderKind folderKind, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        List<DriveFolderReference> filteredFolders = folders.OrderBy(folder => getFolderSortOrder(folder)).ThenBy(folder => folder.TeamName).ThenBy(folder => folder.DisplayName).ToList();
        return filteredFolders;
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await mPermissionService.GetReadableDriveFoldersAsync(userSession, folderKind, cancellationToken);
        List<Task<IReadOnlyList<DriveFileItem>>> fileTasks = new List<Task<IReadOnlyList<DriveFileItem>>>();
        foreach (DriveFolderReference folder in folders)
        {
            fileTasks.Add(loadFolderFilesAsync(folder, keyword, cancellationToken));
        }

        IReadOnlyList<DriveFileItem>[] fileGroups = await Task.WhenAll(fileTasks);
        List<DriveFileItem> mergedFiles = new List<DriveFileItem>();
        foreach (IReadOnlyList<DriveFileItem> fileGroup in fileGroups)
        {
            mergedFiles.AddRange(fileGroup);
        }

        List<DriveFileItem> orderedFiles = mergedFiles.OrderByDescending(file => file.ModifiedTime).ThenBy(file => file.Name).ToList();
        await mUploadAuditLogService.AttachUploadMetadataAsync(orderedFiles, cancellationToken);
        return orderedFiles;
    }

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

    public void InvalidateRecentFiles()
    {
        mMemoryCache.Set(RECENT_FILES_CACHE_VERSION_KEY, Guid.NewGuid().ToString("N"), RECENT_FILES_CACHE_VERSION_DURATION);
        mChangeVersionService.Bump(ChangeVersionKeys.DRIVE_FILES);
    }

    public void InvalidateRecentFiles(string folderID)
    {
        InvalidateRecentFiles();
        mChangeVersionService.Bump(ChangeVersionKeys.CreateDriveFolderKey(folderID));
    }

    public void SuppressRecentUploadMerge(string fileID)
    {
        if (string.IsNullOrWhiteSpace(fileID))
        {
            return;
        }

        mMemoryCache.Set(createRecentUploadSuppressionCacheKey(fileID), true, RECENT_UPLOAD_MERGE_WINDOW);
    }

    public async Task<IReadOnlyList<DriveFileItem>> GetFolderFilesAsync(UserSession userSession, EDriveFolderKind folderKind, string rootFolderID, string folderID, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFolderReference> folders = await GetReadableFoldersAsync(userSession, folderKind, cancellationToken);
        DriveFolderReference? rootFolderOrNull = folders.FirstOrDefault(folder => folder.FolderID == rootFolderID);
        if (rootFolderOrNull == null)
        {
            return new List<DriveFileItem>();
        }

        bool canBrowseFolder = await canBrowseFolderInsideRootAsync(rootFolderOrNull, folderID, cancellationToken);
        if (canBrowseFolder == false)
        {
            return new List<DriveFileItem>();
        }

        EDriveFolderUploadAccess uploadAccess = rootFolderOrNull.CanUpload ? EDriveFolderUploadAccess.UploadAllowed : EDriveFolderUploadAccess.UploadDenied;
        IReadOnlyList<DriveFileItem> files = await loadFolderFilesAsync(new DriveFolderReference(folderID, rootFolderOrNull.DisplayName, rootFolderOrNull.FolderKind, rootFolderOrNull.TeamID, rootFolderOrNull.TeamKey, rootFolderOrNull.TeamName, uploadAccess), keyword, cancellationToken);
        List<DriveFileItem> orderedFiles = orderFilesForBrowser(files);
        await mUploadAuditLogService.AttachUploadMetadataAsync(orderedFiles, cancellationToken);
        return orderedFiles;
    }

    private async Task<IReadOnlyList<DriveFileItem>> loadFolderFilesAsync(DriveFolderReference folder, string keyword, CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> files;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            files = await mDriveService.ListFilesAsync(folder.FolderID, cancellationToken);
        }
        else
        {
            files = await mDriveService.SearchFilesAsync(folder.FolderID, keyword, cancellationToken);
        }

        List<DriveFileItem> mergedFiles = files.ToList();
        foreach (DriveFileItem file in mergedFiles)
        {
            applyFolderContext(file, folder);
        }

        await mergeRecentUploadedFilesAsync(folder, keyword, mergedFiles, cancellationToken);
        return mergedFiles;
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

    private static void applyFolderContext(DriveFileItem file, DriveFolderReference folder)
    {
        file.TeamKey = folder.TeamKey;
        file.TeamName = folder.TeamName;
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            file.TeamName = folder.DisplayName;
        }

        file.FolderDisplayName = folder.DisplayName;
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

    private static bool matchesKeyword(string fileName, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return fileName.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string inferMimeTypeFromFileName(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        return string.Empty;
    }

    private async Task<bool> canBrowseFolderInsideRootAsync(DriveFolderReference rootFolder, string folderID, CancellationToken cancellationToken)
    {
        if (folderID == rootFolder.FolderID)
        {
            return true;
        }

        DriveFileMetadata? folderMetadataOrNull = await mDriveService.GetFileOrNullAsync(folderID, cancellationToken);
        if (folderMetadataOrNull == null || folderMetadataOrNull.MimeType != GOOGLE_DRIVE_FOLDER_MIME_TYPE)
        {
            return false;
        }

        IReadOnlySet<string> ancestorFolderIDs = await mDriveService.GetAncestorFolderIDsAsync(folderID, cancellationToken);
        return ancestorFolderIDs.Contains(rootFolder.FolderID);
    }

    private List<DriveFileItem> orderFilesForBrowser(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> orderedFiles = files.OrderBy(file => file.IsFolder ? 0 : 1).ThenBy(file => file.IsFolder ? file.Name : string.Empty).ThenByDescending(file => file.ModifiedTime).ThenBy(file => file.Name).ToList();
        return orderedFiles;
    }

    private int getFolderSortOrder(DriveFolderReference folder)
    {
        if (folder.FolderKind != EDriveFolderKind.TeamApproved)
        {
            return 1;
        }

        if (folder.IsManagementTeam)
        {
            return 0;
        }

        return 1;
    }

    private string createRecentFilesCacheKey(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter)
    {
        return createRecentFilesCacheKey("DriveFiles", userSession, folderKind, maximumCount, managementTeamFilter);
    }

    private string createRecentUploadedFilesCacheKey(UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter)
    {
        return createRecentFilesCacheKey("UploadedFiles", userSession, folderKind, maximumCount, managementTeamFilter);
    }

    private string createRecentFilesCacheKey(string sourceName, UserSession userSession, EDriveFolderKind folderKind, int maximumCount, EManagementTeamFilter managementTeamFilter)
    {
        string teamIDText = "none";
        if (userSession.TeamID.HasValue)
        {
            teamIDText = userSession.TeamID.Value.ToString(CultureInfo.InvariantCulture);
        }

        string managementTeamFilterText = managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam ? "exclude-management" : "include-management";
        return "OrganizationHub:DriveBrowser:RecentFiles:" + sourceName + ":" + getRecentFilesCacheVersion() + ":" + userSession.UserID.ToString(CultureInfo.InvariantCulture) + ":" + userSession.Role + ":" + teamIDText + ":" + folderKind + ":" + maximumCount.ToString(CultureInfo.InvariantCulture) + ":" + managementTeamFilterText;
    }

    private static bool isManagementFile(DriveFileItem file)
    {
        return string.Equals(file.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }

    private bool isRecentUploadMergeSuppressed(string fileID)
    {
        return mMemoryCache.Get<bool>(createRecentUploadSuppressionCacheKey(fileID));
    }

    private static string createRecentUploadSuppressionCacheKey(string fileID)
    {
        return RECENT_UPLOAD_SUPPRESSION_PREFIX + fileID.Trim();
    }

    private string getRecentFilesCacheVersion()
    {
        string? cachedVersionOrNull = mMemoryCache.Get<string>(RECENT_FILES_CACHE_VERSION_KEY);
        if (string.IsNullOrWhiteSpace(cachedVersionOrNull) == false)
        {
            return cachedVersionOrNull;
        }

        string version = Guid.NewGuid().ToString("N");
        mMemoryCache.Set(RECENT_FILES_CACHE_VERSION_KEY, version, RECENT_FILES_CACHE_VERSION_DURATION);
        return version;
    }

    private List<DriveFileItem> cloneDriveFileItems(IReadOnlyList<DriveFileItem> files)
    {
        List<DriveFileItem> clonedFiles = new List<DriveFileItem>();
        foreach (DriveFileItem file in files)
        {
            clonedFiles.Add(cloneDriveFileItem(file));
        }

        return clonedFiles;
    }

    private DriveFileItem cloneDriveFileItem(DriveFileItem file)
    {
        DriveFileItem clonedFile = new DriveFileItem();
        clonedFile.ID = file.ID;
        clonedFile.Name = file.Name;
        clonedFile.MimeType = file.MimeType;
        clonedFile.CreatedTime = file.CreatedTime;
        clonedFile.ModifiedTime = file.ModifiedTime;
        clonedFile.IconLink = file.IconLink;
        clonedFile.ThumbnailLink = file.ThumbnailLink;
        clonedFile.IsFolder = file.IsFolder;
        clonedFile.TeamKey = file.TeamKey;
        clonedFile.TeamName = file.TeamName;
        clonedFile.FolderDisplayName = file.FolderDisplayName;
        clonedFile.UploadedByDisplayName = file.UploadedByDisplayName;
        clonedFile.UploadedAt = file.UploadedAt;
        clonedFile.CanPreviewInSite = file.CanPreviewInSite;
        clonedFile.Size = file.Size;
        return clonedFile;
    }
}
