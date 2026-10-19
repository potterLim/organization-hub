using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OrganizationHub.Web.Services;

internal static class ResourceCreationGuard
{
    private static readonly TimeSpan ROLLBACK_TIMEOUT = TimeSpan.FromSeconds(15);

    public static async Task<T> PersistMetadataAsync<T>(Func<Task<T>> saveAsync, Func<CancellationToken, Task> rollbackAsync, string resourceId, ILogger logger)
    {
        try
        {
            return await saveAsync();
        }
        catch (Exception storageError)
        {
            try
            {
                // A cancelled browser request must not cancel compensation of an already-created item.
                using (CancellationTokenSource rollback = new CancellationTokenSource(ROLLBACK_TIMEOUT))
                {
                    await rollbackAsync(rollback.Token);
                }
            }
            catch (Google.GoogleApiException remoteError) when (remoteError.HttpStatusCode == HttpStatusCode.NotFound)
            {
                throw new ResourceCreationException(resourceId, false, storageError);
            }
            catch (Exception rollbackError)
            {
                AggregateException failures = new AggregateException(storageError, rollbackError);
                logger.LogError(failures, "Untracked resource {ResourceId} needs administrator recovery.", resourceId);
                throw new ResourceCreationException(resourceId, true, failures);
            }
            throw new ResourceCreationException(resourceId, false, storageError);
        }
    }
}
