using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal interface IRecentUploadedFileCacheRefreshQueue
{
    bool TryEnqueue(RecentUploadedFileCacheRefreshRequest request);
}
