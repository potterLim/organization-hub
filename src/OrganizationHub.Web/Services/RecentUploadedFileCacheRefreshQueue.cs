using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class RecentUploadedFileCacheRefreshQueue : IRecentUploadedFileCacheRefreshWorkQueue
{
    private const int CHANNEL_CAPACITY = 100;

    private readonly Channel<RecentUploadedFileCacheRefreshRequest> mChannel;
    private readonly ILogger<RecentUploadedFileCacheRefreshQueue> mLogger;

    public RecentUploadedFileCacheRefreshQueue(ILogger<RecentUploadedFileCacheRefreshQueue> logger)
    {
        BoundedChannelOptions channelOptions = new BoundedChannelOptions(CHANNEL_CAPACITY);
        channelOptions.FullMode = BoundedChannelFullMode.Wait;
        channelOptions.SingleReader = true;
        channelOptions.SingleWriter = false;

        mChannel = Channel.CreateBounded<RecentUploadedFileCacheRefreshRequest>(channelOptions);
        mLogger = logger;
    }

    bool IRecentUploadedFileCacheRefreshQueue.TryEnqueue(RecentUploadedFileCacheRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool isQueued = mChannel.Writer.TryWrite(request);
        if (isQueued == false)
        {
            mLogger.LogWarning("Recent uploaded file cache refresh request was not queued. CacheKey: {CacheKey}", request.CacheKey);
        }

        return isQueued;
    }

    async Task<RecentUploadedFileCacheRefreshRequest> IRecentUploadedFileCacheRefreshWorkQueue.DequeueAsync(CancellationToken cancellationToken)
    {
        RecentUploadedFileCacheRefreshRequest request = await mChannel.Reader.ReadAsync(cancellationToken);
        return request;
    }
}
