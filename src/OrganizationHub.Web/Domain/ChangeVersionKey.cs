using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct ChangeVersionKey
{
    private string Value
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

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Change version key is required.");
        }

        return Value;
    }
}
