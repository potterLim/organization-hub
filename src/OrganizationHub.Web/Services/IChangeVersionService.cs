using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IChangeVersionService
{
    long GetVersion(string key);
    ChangeVersionSnapshot GetSnapshot(IReadOnlyList<string> keys);
    bool HasChanged(ChangeVersionSnapshot snapshot);
    bool HasActiveChange(IReadOnlyList<string> keys);
    IDisposable BeginChange(IReadOnlyList<string> keys);
    Task<ChangeVersionReadResult<T>> ReadWithStableSnapshotAsync<T>(IReadOnlyList<string> keys, Func<CancellationToken, Task<T>> readValue, CancellationToken cancellationToken);
    void Bump(string key);
    void BumpMany(IReadOnlyList<string> keys);
}
