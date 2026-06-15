using System;
using System.Globalization;

namespace OrganizationHub.Web.Domain;

public readonly record struct RecentItemLimit
{
    public const int MINIMUM_VALUE = 1;
    public const int MAXIMUM_VALUE = 200;

    private int Value
    {
        get;
    }

    public RecentItemLimit(int limitValue)
    {
        if (limitValue < MINIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(limitValue), "Recent item limit must be at least 1.");
        }

        if (limitValue > MAXIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(limitValue), "Recent item limit cannot exceed 200.");
        }

        Value = limitValue;
    }

    public int GetRequiredValue()
    {
        if (Value < MINIMUM_VALUE || Value > MAXIMUM_VALUE)
        {
            throw new InvalidOperationException("Recent item limit is outside the supported range.");
        }

        return Value;
    }

    public override string ToString()
    {
        return GetRequiredValue().ToString(CultureInfo.InvariantCulture);
    }
}
