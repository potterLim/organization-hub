using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed partial class DriveBrowserService
{
    public void InvalidateRecentFiles()
    {
        mMemoryCache.Set(RECENT_FILES_CACHE_VERSION_KEY, Guid.NewGuid().ToString("N"), RECENT_FILES_CACHE_VERSION_DURATION);
        mChangeVersionService.Bump(ChangeVersionKeys.DRIVE_FILES);
    }

    public void InvalidateRecentFiles(DriveFolderId folderId)
    {
        InvalidateRecentFiles();
        mChangeVersionService.Bump(ChangeVersionKeys.CreateDriveFolderKey(folderId));
    }

    public void SuppressRecentUploadMerge(DriveItemId fileId)
    {
        if (fileId.HasValue == false)
        {
            return;
        }

        mMemoryCache.Set(createRecentUploadSuppressionCacheKey(fileId), true, RECENT_UPLOAD_MERGE_WINDOW);
    }

    private string createRecentFilesCacheKey(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter
    )
    {
        return createRecentFilesCacheKey("DriveFiles", userSession, folderKind, maximumCount, managementTeamFilter);
    }

    private string createRecentUploadedFilesCacheKey(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter
    )
    {
        return createRecentFilesCacheKey("UploadedFiles", userSession, folderKind, maximumCount, managementTeamFilter);
    }

    private string createRecentFilesCacheKey(
        string sourceName,
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter
    )
    {
        string teamIdText = "none";
        if (userSession.HasTeam)
        {
            teamIdText = userSession.GetRequiredOrganizationTeamId().GetRequiredValue().ToString(CultureInfo.InvariantCulture);
        }

        string managementTeamFilterText = managementTeamFilter == EManagementTeamFilter.ExcludeManagementTeam ? "exclude-management" : "include-management";
        List<string> cacheKeyParts = new List<string>
        {
            "OrganizationHub",
            "DriveBrowser",
            "RecentFiles",
            sourceName,
            getRecentFilesCacheVersion(),
            userSession.UserAccountId.GetRequiredValue().ToString(CultureInfo.InvariantCulture),
            userSession.StoredCompatibilityRole.ToString(),
            teamIdText,
            folderKind.ToString(),
            maximumCount.GetRequiredValue().ToString(CultureInfo.InvariantCulture),
            managementTeamFilterText
        };
        return string.Join(":", cacheKeyParts);
    }

    private bool isRecentUploadMergeSuppressed(DriveItemId fileId)
    {
        return mMemoryCache.Get<bool>(createRecentUploadSuppressionCacheKey(fileId));
    }

    private static string createRecentUploadSuppressionCacheKey(DriveItemId fileId)
    {
        return RECENT_UPLOAD_SUPPRESSION_PREFIX + fileId.GetRequiredValue();
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
        clonedFile.Id = file.Id;
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
        clonedFile.UploadedByTeamName = file.UploadedByTeamName;
        clonedFile.UploadedByPositionName = file.UploadedByPositionName;
        clonedFile.UploadedAt = file.UploadedAt;
        clonedFile.CanPreviewInSite = file.CanPreviewInSite;
        clonedFile.Size = file.Size;
        return clonedFile;
    }
}
