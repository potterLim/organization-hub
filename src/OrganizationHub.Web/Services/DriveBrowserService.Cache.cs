using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class DriveBrowserService
{
    public void InvalidateRecentFiles()
    {
        mMemoryCache.Set(RECENT_FILES_CACHE_VERSION_KEY, Guid.NewGuid().ToString("N"), RECENT_FILES_CACHE_VERSION_DURATION);
        mChangeVersionService.Bump(ChangeVersionKeys.DRIVE_FILES);
    }

    public void InvalidateRecentFiles(string folderID)
    {
        InvalidateRecentFiles();
        mChangeVersionService.Bump(ChangeVersionKeys.CreateDriveFolderKey(new DriveFolderID(folderID)));
    }

    public void SuppressRecentUploadMerge(string fileID)
    {
        if (string.IsNullOrWhiteSpace(fileID))
        {
            return;
        }

        mMemoryCache.Set(createRecentUploadSuppressionCacheKey(fileID), true, RECENT_UPLOAD_MERGE_WINDOW);
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
