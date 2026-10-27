using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrganizationHub.Web.Services;

internal sealed class AuditRecoveryWorker : BackgroundService
{
    private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory mScopeFactory;
    private readonly ILogger<AuditRecoveryWorker> mLogger;

    public AuditRecoveryWorker(IServiceScopeFactory scopeFactory, ILogger<AuditRecoveryWorker> logger)
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
                    await scope.ServiceProvider.GetRequiredService<AuditRecoveryService>().RecoverAsync(stoppingToken);
                }
                await Task.Delay(POLL_INTERVAL, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                mLogger.LogWarning(exception, "Audit recovery will be retried.");
                await Task.Delay(POLL_INTERVAL, stoppingToken);
            }
        }
    }
}

