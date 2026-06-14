using System;
using Microsoft.Extensions.Caching.Memory;

namespace OrganizationHub.Web.Services;

public sealed partial class DriveBrowserService : IDriveBrowserService
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
}
