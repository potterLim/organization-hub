using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

public partial class ApprovalInbox
{
    private static readonly TimeSpan PUBLICATION_REFRESH_INTERVAL = TimeSpan.FromSeconds(5);
    private readonly CancellationTokenSource mRefreshCancellation = new CancellationTokenSource();
    private Task? mRefreshTaskOrNull;

    public async ValueTask DisposeAsync()
    {
        await mRefreshCancellation.CancelAsync();
        if (mRefreshTaskOrNull != null)
        {
            await mRefreshTaskOrNull;
        }
        mRefreshCancellation.Dispose();
    }
    private async Task refreshPublicationsAsync()
    {
        using (PeriodicTimer timer = new PeriodicTimer(PUBLICATION_REFRESH_INTERVAL))
        {
            try
            {
                while (await timer.WaitForNextTickAsync(mRefreshCancellation.Token))
                {
                    await InvokeAsync(refreshPendingPublicationsAsync);
                }
            }
            catch (OperationCanceledException) when (mRefreshCancellation.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task refreshPendingPublicationsAsync()
    {
        if (mProcessingApprovalIds.Count > 0 || mPendingApprovals.Any(item => item.Status == EApprovalRequestStatus.Publishing) == false)
        {
            return;
        }
        try
        {
            await loadDataAsync();
            StateHasChanged();
        }
        catch (Exception exception) when (mRefreshCancellation.IsCancellationRequested == false)
        {
            Logger.LogWarning(exception, "Approval status refresh will be retried.");
        }
    }


}
