using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class ChangeVersionReadResult<T>
{
    public T Value
    {
        get; private init;
    }
    public ChangeVersionSnapshot Snapshot
    {
        get; private init;
    }
    public bool IsStable
    {
        get; private init;
    }

    public ChangeVersionReadResult(T value, ChangeVersionSnapshot snapshot, EChangeVersionStability stability)
    {
        Value = value;
        Snapshot = snapshot;
        IsStable = stability == EChangeVersionStability.Stable;
    }
}
