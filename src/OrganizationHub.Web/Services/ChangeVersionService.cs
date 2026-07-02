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
    private const long INITIAL_VERSION = 0;
    private const long FIRST_VERSION = 1;
    private const long VERSION_INCREMENT = 1;
    private const int NO_ACTIVE_CHANGE_COUNT = 0;
    private const int FIRST_ACTIVE_CHANGE_COUNT = 1;
    private const int ACTIVE_CHANGE_COUNT_INCREMENT = 1;
    private const int MAX_STABLE_READ_ATTEMPTS = 8;

    private readonly ConcurrentDictionary<string, long> mVersions = new ConcurrentDictionary<string, long>();
    private readonly ConcurrentDictionary<string, int> mActiveChangeCounts = new ConcurrentDictionary<string, int>();

    public long GetVersion(ChangeVersionKey key)
    {
        if (key.HasValue == false)
        {
            return INITIAL_VERSION;
        }

        long version;
        string requiredKey = key.GetRequiredValue();
        if (mVersions.TryGetValue(requiredKey, out version))
        {
            return version;
        }

        return INITIAL_VERSION;
    }

    public ChangeVersionSnapshot GetSnapshot(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        ChangeVersionSnapshot snapshot = new ChangeVersionSnapshot();
        foreach (ChangeVersionKey key in normalizeKeys(changeVersionKeys))
        {
            snapshot.Versions[key] = GetVersion(key);
        }

        return snapshot;
    }

    public bool HasChanged(ChangeVersionSnapshot snapshot)
    {
        foreach (KeyValuePair<ChangeVersionKey, long> version in snapshot.Versions)
        {
            if (GetVersion(version.Key) != version.Value)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasActiveChange(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        foreach (ChangeVersionKey key in normalizeKeys(changeVersionKeys))
        {
            int activeChangeCount;
            string requiredKey = key.GetRequiredValue();
            if (mActiveChangeCounts.TryGetValue(requiredKey, out activeChangeCount) && activeChangeCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    public IDisposable BeginChange(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        List<ChangeVersionKey> normalizedKeys = normalizeKeys(changeVersionKeys);
        foreach (ChangeVersionKey key in normalizedKeys)
        {
            mActiveChangeCounts.AddOrUpdate(key.GetRequiredValue(), FIRST_ACTIVE_CHANGE_COUNT, (_, currentCount) => currentCount + ACTIVE_CHANGE_COUNT_INCREMENT);
        }

        BumpMany(normalizedKeys);
        return new ChangeScope(this, normalizedKeys);
    }

    public async Task<ChangeVersionReadResult<T>> ReadWithStableSnapshotAsync<T>(
        IReadOnlyList<ChangeVersionKey> changeVersionKeys,
        Func<CancellationToken, Task<T>> readValue,
        CancellationToken cancellationToken
    )
    {
        List<ChangeVersionKey> normalizedKeys = normalizeKeys(changeVersionKeys);
        ChangeVersionSnapshot latestSnapshot = GetSnapshot(normalizedKeys);
        ChangeVersionReadResult<T>? latestReadResultOrNull = null;
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
            T readValueResult = await readValue(cancellationToken);
            latestReadResultOrNull = new ChangeVersionReadResult<T>(readValueResult, latestSnapshot, EChangeVersionStability.Unstable);
            readAttemptCount++;

            if (HasChanged(latestSnapshot) == false && HasActiveChange(normalizedKeys) == false)
            {
                return new ChangeVersionReadResult<T>(readValueResult, latestSnapshot, EChangeVersionStability.Stable);
            }

            await Task.Delay(STABLE_READ_RETRY_DELAY, cancellationToken);
        }

        if (latestReadResultOrNull != null)
        {
            return latestReadResultOrNull;
        }

        latestSnapshot = GetSnapshot(normalizedKeys);
        T finalReadValue = await readValue(cancellationToken);
        return new ChangeVersionReadResult<T>(finalReadValue, latestSnapshot, EChangeVersionStability.Unstable);
    }

    public void Bump(ChangeVersionKey key)
    {
        if (key.HasValue == false)
        {
            return;
        }

        mVersions.AddOrUpdate(key.GetRequiredValue(), FIRST_VERSION, (_, currentVersion) => currentVersion + VERSION_INCREMENT);
    }

    public void BumpMany(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        foreach (ChangeVersionKey key in normalizeKeys(changeVersionKeys))
        {
            Bump(key);
        }
    }

    private static List<ChangeVersionKey> normalizeKeys(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        return changeVersionKeys.Where(key => key.HasValue).Distinct().ToList();
    }

    private void finishChange(IReadOnlyList<ChangeVersionKey> changeVersionKeys)
    {
        BumpMany(changeVersionKeys);
        foreach (ChangeVersionKey key in normalizeKeys(changeVersionKeys))
        {
            mActiveChangeCounts.AddOrUpdate(key.GetRequiredValue(), NO_ACTIVE_CHANGE_COUNT, (_, currentCount) => Math.Max(NO_ACTIVE_CHANGE_COUNT, currentCount - ACTIVE_CHANGE_COUNT_INCREMENT));
        }
    }

    private sealed class ChangeScope : IDisposable
    {
        private readonly ChangeVersionService mChangeVersionService;
        private readonly IReadOnlyList<ChangeVersionKey> mChangeVersionKeys;
        private bool mIsDisposed;

        public ChangeScope(ChangeVersionService changeVersionService, IReadOnlyList<ChangeVersionKey> changeVersionKeys)
        {
            mChangeVersionService = changeVersionService;
            mChangeVersionKeys = changeVersionKeys;
        }

        public void Dispose()
        {
            if (mIsDisposed)
            {
                return;
            }

            mIsDisposed = true;
            mChangeVersionService.finishChange(mChangeVersionKeys);
        }
    }
}
