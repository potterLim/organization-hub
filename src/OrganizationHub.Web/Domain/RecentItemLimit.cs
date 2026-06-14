using System;
using System.Globalization;

namespace OrganizationHub.Web.Domain;

public readonly record struct RecentItemLimit
{
    public const int MINIMUM_VALUE = 1;
    public const int MAXIMUM_VALUE = 200;

    public int Value
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

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
