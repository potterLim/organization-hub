using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal interface IRecentUploadedFileCacheRefreshWorkQueue : IRecentUploadedFileCacheRefreshQueue
{
    Task<RecentUploadedFileCacheRefreshRequest> DequeueAsync(CancellationToken cancellationToken);
}
