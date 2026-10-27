using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Services;

internal static class ResourceDeletionConfirmation
{
    public static async Task ExecuteAsync(Func<Task> deleteAsync, Func<Task<bool>> isDeletedAsync, CancellationToken cancellationToken)
    {
        try
        {
            await deleteAsync();
        }
        catch (ResourceEditConflictException)
        {
            throw;
        }
        catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.Gone)
        {
            // DELETE is idempotent: an item already absent has the requested outcome.
        }
        catch (Google.GoogleApiException exception) when ((int)exception.HttpStatusCode >= 400 && (int)exception.HttpStatusCode < 500 && exception.HttpStatusCode != HttpStatusCode.RequestTimeout && exception.HttpStatusCode != HttpStatusCode.NotFound)
        {
            throw;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested == false)
        {
            bool isDeleted;
            try
            {
                isDeleted = await isDeletedAsync();
            }
            catch (Exception readException)
            {
                throw new ResourceDeletionUncertainException(new AggregateException(exception, readException));
            }
            if (isDeleted == false)
            {
                throw;
            }
        }
    }

    public static async Task<bool> IsDeletedAsync(Func<Task<bool>> readDeletedStateAsync, Func<Task> ensureContainerReadableAsync)
    {
        try
        {
            return await readDeletedStateAsync();
        }
        catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound || exception.HttpStatusCode == HttpStatusCode.Gone)
        {
            if (exception.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // A hidden calendar or task list can also return 404. Verify read access first.
                await ensureContainerReadableAsync();
            }
            return true;
        }
    }
}
