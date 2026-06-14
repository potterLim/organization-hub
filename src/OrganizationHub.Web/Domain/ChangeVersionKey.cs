using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ChangeVersionKey
{
    public string Value
    {
        get;
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public ChangeVersionKey(string changeVersionKey)
    {
        if (string.IsNullOrWhiteSpace(changeVersionKey))
        {
            throw new ArgumentException("Change version key cannot be empty.", nameof(changeVersionKey));
        }

        Value = changeVersionKey.Trim();
    }

    public override string ToString()
    {
        if (Value == null)
        {
            return string.Empty;
        }

        return Value;
    }
}
