using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

internal static class RecentUploadedFileCachePolicy
{
    private const int SNAPSHOT_MULTIPLIER = 4;

    public static readonly TimeSpan CACHE_DURATION = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan BACKGROUND_VALIDATION_TIMEOUT = TimeSpan.FromSeconds(12);

    public static RecentItemLimit CreateSnapshotLimit(RecentItemLimit maximumCount)
    {
        int expandedCount = maximumCount.GetRequiredValue() * SNAPSHOT_MULTIPLIER;
        int boundedCount = Math.Min(expandedCount, RecentItemLimit.MAXIMUM_VALUE);
        return new RecentItemLimit(boundedCount);
    }
}
