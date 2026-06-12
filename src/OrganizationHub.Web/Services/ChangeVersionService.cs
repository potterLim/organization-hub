using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class ChangeVersionService : IChangeVersionService
{
    private static readonly TimeSpan STABLE_READ_RETRY_DELAY = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MAX_ACTIVE_CHANGE_WAIT = TimeSpan.FromSeconds(30);
    private const int MAX_STABLE_READ_ATTEMPTS = 8;

    private readonly ConcurrentDictionary<string, long> mVersions = new ConcurrentDictionary<string, long>();
    private readonly ConcurrentDictionary<string, int> mActiveChangeCounts = new ConcurrentDictionary<string, int>();

    public long GetVersion(string key)
    {
        long version;
        if (mVersions.TryGetValue(key, out version))
        {
            return version;
        }

        return 0;
    }

    public ChangeVersionSnapshot GetSnapshot(IReadOnlyList<string> keys)
    {
        ChangeVersionSnapshot snapshot = new ChangeVersionSnapshot();
        foreach (string key in keys)
        {
            snapshot.Versions[key] = GetVersion(key);
        }

        return snapshot;
    }

    public bool HasChanged(ChangeVersionSnapshot snapshot)
    {
        foreach (KeyValuePair<string, long> version in snapshot.Versions)
        {
            if (GetVersion(version.Key) != version.Value)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasActiveChange(IReadOnlyList<string> keys)
    {
        foreach (string key in normalizeKeys(keys))
        {
            int activeChangeCount;
            if (mActiveChangeCounts.TryGetValue(key, out activeChangeCount) && activeChangeCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    public IDisposable BeginChange(IReadOnlyList<string> keys)
    {
        List<string> normalizedKeys = normalizeKeys(keys);
        foreach (string key in normalizedKeys)
        {
            mActiveChangeCounts.AddOrUpdate(key, 1, (_, currentCount) => currentCount + 1);
        }

        BumpMany(normalizedKeys);
        return new ChangeScope(this, normalizedKeys);
    }

    public async Task<ChangeVersionReadResult<T>> ReadWithStableSnapshotAsync<T>(IReadOnlyList<string> keys, Func<CancellationToken, Task<T>> readValue, CancellationToken cancellationToken)
    {
        List<string> normalizedKeys = normalizeKeys(keys);
        T latestValue = default!;
        ChangeVersionSnapshot latestSnapshot = GetSnapshot(normalizedKeys);
        bool hasValue = false;
        DateTime activeChangeWaitDeadline = DateTime.UtcNow.Add(MAX_ACTIVE_CHANGE_WAIT);

        int readAttemptCount = 0;
        while (readAttemptCount < MAX_STABLE_READ_ATTEMPTS)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasActiveChange(normalizedKeys))
            {
                if (DateTime.UtcNow >= activeChangeWaitDeadline)
                {
                    break;
                }

                await Task.Delay(STABLE_READ_RETRY_DELAY, cancellationToken);
                continue;
            }

            latestSnapshot = GetSnapshot(normalizedKeys);
            latestValue = await readValue(cancellationToken);
            hasValue = true;
            readAttemptCount++;

            if (HasChanged(latestSnapshot) == false && HasActiveChange(normalizedKeys) == false)
            {
                return new ChangeVersionReadResult<T>(latestValue, latestSnapshot, EChangeVersionStability.Stable);
            }

            await Task.Delay(STABLE_READ_RETRY_DELAY, cancellationToken);
        }

        if (hasValue == false)
        {
            latestSnapshot = GetSnapshot(normalizedKeys);
            latestValue = await readValue(cancellationToken);
        }

        return new ChangeVersionReadResult<T>(latestValue, latestSnapshot, EChangeVersionStability.Unstable);
    }

    public void Bump(string key)
    {
        mVersions.AddOrUpdate(key, 1, (_, currentVersion) => currentVersion + 1);
    }

    public void BumpMany(IReadOnlyList<string> keys)
    {
        foreach (string key in keys)
        {
            Bump(key);
        }
    }

    private static List<string> normalizeKeys(IReadOnlyList<string> keys)
    {
        return keys.Where(key => string.IsNullOrWhiteSpace(key) == false).Select(key => key.Trim()).Distinct().ToList();
    }

    private void finishChange(IReadOnlyList<string> keys)
    {
        BumpMany(keys);
        foreach (string key in keys)
        {
            mActiveChangeCounts.AddOrUpdate(key, 0, (_, currentCount) => Math.Max(0, currentCount - 1));
        }
    }

    private sealed class ChangeScope : IDisposable
    {
        private readonly ChangeVersionService mChangeVersionService;
        private readonly IReadOnlyList<string> mKeys;
        private bool mIsDisposed;

        public ChangeScope(ChangeVersionService changeVersionService, IReadOnlyList<string> keys)
        {
            mChangeVersionService = changeVersionService;
            mKeys = keys;
        }

        public void Dispose()
        {
            if (mIsDisposed)
            {
                return;
            }

            mIsDisposed = true;
            mChangeVersionService.finishChange(mKeys);
        }
    }
}
