using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

internal sealed class RecentUploadedFileCacheRefreshRequest
{
    public required string CacheKey
    {
        get; init;
    }

    public required IReadOnlyList<UploadAuditLogItem> SnapshotUploads
    {
        get; init;
    }

    public required UserSession UserSession
    {
        get; init;
    }

    public required EDriveFolderKind FolderKind
    {
        get; init;
    }

    public required RecentItemLimit MaximumCount
    {
        get; init;
    }

    public required EManagementTeamFilter ManagementTeamFilter
    {
        get; init;
    }
}
