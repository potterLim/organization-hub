using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class RecentUploadedFileCacheRefreshWorker : BackgroundService
{
    private readonly IRecentUploadedFileCacheRefreshWorkQueue mQueue;
    private readonly IServiceScopeFactory mServiceScopeFactory;
    private readonly IMemoryCache mMemoryCache;
    private readonly ILogger<RecentUploadedFileCacheRefreshWorker> mLogger;

    public RecentUploadedFileCacheRefreshWorker(
        IRecentUploadedFileCacheRefreshWorkQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache memoryCache,
        ILogger<RecentUploadedFileCacheRefreshWorker> logger
    )
    {
        mQueue = queue;
        mServiceScopeFactory = serviceScopeFactory;
        mMemoryCache = memoryCache;
        mLogger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (stoppingToken.IsCancellationRequested == false)
            {
                RecentUploadedFileCacheRefreshRequest request = await mQueue.DequeueAsync(stoppingToken);
                await refreshRecentUploadedFileCacheAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task refreshRecentUploadedFileCacheAsync(
        RecentUploadedFileCacheRefreshRequest request,
        CancellationToken stoppingToken
    )
    {
        using (CancellationTokenSource timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
        {
            timeoutCancellationTokenSource.CancelAfter(RecentUploadedFileCachePolicy.BACKGROUND_VALIDATION_TIMEOUT);

            try
            {
                using (IServiceScope serviceScope = mServiceScopeFactory.CreateScope())
                {
                    IUploadAuditLogService uploadAuditLogService = serviceScope.ServiceProvider.GetRequiredService<IUploadAuditLogService>();
                    RecentItemLimit snapshotCount = RecentUploadedFileCachePolicy.CreateSnapshotLimit(request.MaximumCount);
                    IReadOnlyList<UploadAuditLogItem> verifiedUploads = await uploadAuditLogService.GetRecentUploadsForReadableRootsAsync(
                        request.UserSession,
                        request.FolderKind,
                        snapshotCount,
                        request.ManagementTeamFilter,
                        timeoutCancellationTokenSource.Token);
                    List<DriveFileItem> refreshedFiles = RecentUploadedFileItemMapper.CreateDriveFileItems(
                        request.SnapshotUploads,
                        verifiedUploads,
                        request.MaximumCount);
                    mMemoryCache.Set(request.CacheKey, refreshedFiles, RecentUploadedFileCachePolicy.CACHE_DURATION);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                mLogger.LogDebug("Recent uploaded file cache refresh timed out. CacheKey: {CacheKey}", request.CacheKey);
            }
            catch (Exception exception)
            {
                mLogger.LogWarning(exception, "Recent uploaded file cache refresh failed. CacheKey: {CacheKey}", request.CacheKey);
            }
        }
    }
}
