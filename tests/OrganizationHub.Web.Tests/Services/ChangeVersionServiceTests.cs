using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class ChangeVersionServiceTests
{
    [Fact]
    public void HasChangedReturnsFalseWhenSnapshotIsCurrent()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.DRIVE_FILES);

        ChangeVersionSnapshot snapshot = service.GetSnapshot(keys);

        Assert.False(service.HasChanged(snapshot));
    }

    [Fact]
    public void HasChangedReturnsTrueAfterTrackedKeyIsBumped()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.TASKS);

        ChangeVersionSnapshot snapshot = service.GetSnapshot(keys);
        service.Bump(ChangeVersionKeys.TASKS);

        Assert.True(service.HasChanged(snapshot));
    }

    [Fact]
    public void SnapshotTracksSpecificFolderKeySeparately()
    {
        ChangeVersionService service = new ChangeVersionService();
        ChangeVersionKey planningFolderKey = ChangeVersionKeys.CreateDriveFolderKey(DriveFolderId.CreateOptional("planning-folder"));
        ChangeVersionKey operationFolderKey = ChangeVersionKeys.CreateDriveFolderKey(DriveFolderId.CreateOptional("operation-folder"));
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(planningFolderKey);

        ChangeVersionSnapshot snapshot = service.GetSnapshot(keys);
        service.Bump(operationFolderKey);

        Assert.False(service.HasChanged(snapshot));

        service.Bump(planningFolderKey);

        Assert.True(service.HasChanged(snapshot));
    }

    [Fact]
    public async Task ReadWithStableSnapshotRetriesWhenVersionChangesDuringReadAsync()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.TASKS);
        int readCount = 0;

        ChangeVersionReadResult<string> result = await service.ReadWithStableSnapshotAsync(
            keys,
            _ =>
            {
                readCount++;
                if (readCount == 1)
                {
                    service.Bump(ChangeVersionKeys.TASKS);
                }

                return Task.FromResult("read-" + readCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            },
            CancellationToken.None);

        Assert.True(result.IsStable);
        Assert.Equal("read-2", result.Value);
        Assert.False(service.HasChanged(result.Snapshot));
    }

    [Fact]
    public async Task ReadWithStableSnapshotWaitsForActiveChangeBeforeReadingAsync()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.DRIVE_FILES);
        int readCount = 0;

        Task<ChangeVersionReadResult<int>> readTask;
        using (IDisposable changeScope = service.BeginChange(keys))
        {
            readTask = service.ReadWithStableSnapshotAsync(
                keys,
                _ =>
                {
                    readCount++;
                    return Task.FromResult(readCount);
                },
                CancellationToken.None);

            await Task.Delay(150);
            Assert.Equal(0, readCount);
        }

        ChangeVersionReadResult<int> result = await readTask;

        Assert.True(result.IsStable);
        Assert.Equal(1, result.Value);
        Assert.False(service.HasActiveChange(keys));
    }

    [Fact]
    public async Task ReadWithStableSnapshotDoesNotSpendReadAttemptsWhileChangeIsActiveAsync()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.DRIVE_FILES);
        int readCount = 0;

        Task<ChangeVersionReadResult<int>> readTask;
        using (IDisposable changeScope = service.BeginChange(keys))
        {
            readTask = service.ReadWithStableSnapshotAsync(
                keys,
                _ =>
                {
                    readCount++;
                    return Task.FromResult(readCount);
                },
                CancellationToken.None);

            await Task.Delay(1200);
            Assert.Equal(0, readCount);
        }

        ChangeVersionReadResult<int> result = await readTask;

        Assert.True(result.IsStable);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task ReadWithStableSnapshotCanBypassLateStaleCacheWhenCacheKeyUsesVersionAsync()
    {
        ChangeVersionService service = new ChangeVersionService();
        ChangeVersionKey folderKey = ChangeVersionKeys.CreateDriveFolderKey(DriveFolderId.CreateOptional("planning-folder"));
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(folderKey);
        Dictionary<string, string> cache = new Dictionary<string, string>();
        string sourceValue = "old";
        int readCount = 0;

        ChangeVersionReadResult<string> result = await service.ReadWithStableSnapshotAsync(
            keys,
            _ =>
            {
                readCount++;
                string cacheKey = folderKey.Value + ":" + service.GetVersion(folderKey).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string? cachedValue;
                if (cache.TryGetValue(cacheKey, out cachedValue))
                {
                    return Task.FromResult(cachedValue);
                }

                if (readCount == 1)
                {
                    using (service.BeginChange(keys))
                    {
                        sourceValue = "new";
                    }

                    cache[cacheKey] = "old";
                    return Task.FromResult("old");
                }

                cache[cacheKey] = sourceValue;
                return Task.FromResult(sourceValue);
            },
            CancellationToken.None);

        Assert.True(result.IsStable);
        Assert.Equal("new", result.Value);
        Assert.Equal(2, readCount);
    }

    [Fact]
    public void BeginChangeBumpsAtStartAndFinish()
    {
        ChangeVersionService service = new ChangeVersionService();
        List<ChangeVersionKey> keys = new List<ChangeVersionKey>();
        keys.Add(ChangeVersionKeys.CALENDAR);
        ChangeVersionSnapshot initialSnapshot = service.GetSnapshot(keys);

        using (IDisposable changeScope = service.BeginChange(keys))
        {
            Assert.True(service.HasActiveChange(keys));
            Assert.True(service.HasChanged(initialSnapshot));
        }

        Assert.False(service.HasActiveChange(keys));
        Assert.Equal(2, service.GetVersion(ChangeVersionKeys.CALENDAR));
    }
}
