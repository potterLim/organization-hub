using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct FileByteLimit
{
    private long Value
    {
        get;
    }

    public FileByteLimit(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long GetRequiredValue()
    {
        return Value > 0 ? Value : throw new InvalidOperationException("A positive file byte limit is required.");
    }
}
