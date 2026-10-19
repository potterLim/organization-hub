using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Tests.Infrastructure;

public sealed class ApprovalSaveInterceptor : SaveChangesInterceptor
{
    private int mShouldFailCompletion;
    private int mShouldPauseDecision;
    private int mShouldFailIntent;

    public TaskCompletionSource<bool> DecisionPaused { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> ContinueDecision { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> CompletionSaved { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    public void FailNextIntent()
    {
        Interlocked.Exchange(ref mShouldFailIntent, 1);
    }

    public void FailNextCompletion()
    {
        Interlocked.Exchange(ref mShouldFailCompletion, 1);
    }

    public void PauseNextDecision()
    {
        Interlocked.Exchange(ref mShouldPauseDecision, 1);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        DbContext? dbContextOrNull = eventData.Context;
        if (dbContextOrNull == null)
        {
            return result;
        }

        bool hasModifiedRequest = dbContextOrNull.ChangeTracker.Entries<ApprovalRequest>().Any(entry => entry.State == EntityState.Modified);
        if (hasModifiedRequest && Interlocked.Exchange(ref mShouldPauseDecision, 0) == 1)
        {
            DecisionPaused.TrySetResult(true);
            await ContinueDecision.Task.WaitAsync(cancellationToken);
        }

        bool hasCompletion = dbContextOrNull.ChangeTracker.Entries<ApprovalRequest>().Any(entry => entry.State == EntityState.Modified && entry.Entity.Status == EApprovalRequestStatus.Approved);
        bool hasIntent = dbContextOrNull.ChangeTracker.Entries<ApprovalPublication>().Any(entry => entry.State == EntityState.Added);
        if (hasIntent && Interlocked.Exchange(ref mShouldFailIntent, 0) == 1)
        {
            throw new DbUpdateException("Injected DB failure before publication intent commit.");
        }

        if (hasCompletion && Interlocked.Exchange(ref mShouldFailCompletion, 0) == 1)
        {
            throw new DbUpdateException("Injected DB failure after successful publication.");
        }

        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        DbContext? dbContextOrNull = eventData.Context;
        if (dbContextOrNull != null && dbContextOrNull.ChangeTracker.Entries<ApprovalRequest>().Any(entry => entry.Entity.Status == EApprovalRequestStatus.Approved))
        {
            CompletionSaved.TrySetResult(true);
        }

        return new ValueTask<int>(result);
    }
}
