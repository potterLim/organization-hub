using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Tests.Infrastructure;

internal sealed class SettingSaveBarrier : SaveChangesInterceptor
{
    private readonly TaskCompletionSource mPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource mContinue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private int mHasPaused;

    public Task Paused
    {
        get
        {
            return mPaused.Task;
        }
    }

    public void Release()
    {
        mContinue.TrySetResult();
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null && eventData.Context.ChangeTracker.Entries<GlobalSetting>().Any(entry => entry.State == EntityState.Modified) && Interlocked.Exchange(ref mHasPaused, 1) == 0)
        {
            mPaused.TrySetResult();
            await mContinue.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
