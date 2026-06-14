using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IChangeVersionService
{
    long GetVersion(ChangeVersionKey key);

    ChangeVersionSnapshot GetSnapshot(IReadOnlyList<ChangeVersionKey> changeVersionKeys);

    bool HasChanged(ChangeVersionSnapshot snapshot);

    bool HasActiveChange(IReadOnlyList<ChangeVersionKey> changeVersionKeys);

    IDisposable BeginChange(IReadOnlyList<ChangeVersionKey> changeVersionKeys);

    Task<ChangeVersionReadResult<T>> ReadWithStableSnapshotAsync<T>(
        IReadOnlyList<ChangeVersionKey> changeVersionKeys,
        Func<CancellationToken, Task<T>> readValue,
        CancellationToken cancellationToken);

    void Bump(ChangeVersionKey key);

    void BumpMany(IReadOnlyList<ChangeVersionKey> changeVersionKeys);
}
