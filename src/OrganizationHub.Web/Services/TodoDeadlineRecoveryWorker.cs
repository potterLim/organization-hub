using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrganizationHub.Web.Services;

internal sealed class TodoDeadlineRecoveryWorker : BackgroundService
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory mScopeFactory;
    private readonly ILogger<TodoDeadlineRecoveryWorker> mLogger;

    public TodoDeadlineRecoveryWorker(IServiceScopeFactory scopeFactory, ILogger<TodoDeadlineRecoveryWorker> logger)
    {
        mScopeFactory = scopeFactory;
        mLogger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested == false)
        {
            try
            {
                using (IServiceScope scope = mScopeFactory.CreateScope())
                {
                    await scope.ServiceProvider.GetRequiredService<GoogleTasksTodoService>().RecoverDeadlineChangesAsync(stoppingToken);
                }
                await Task.Delay(POLL_INTERVAL, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                mLogger.LogWarning(exception, "Task deadline recovery will be retried.");
                await Task.Delay(POLL_INTERVAL, stoppingToken);
            }
        }
    }
}
