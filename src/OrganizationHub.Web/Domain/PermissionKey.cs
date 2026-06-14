using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PermissionKey
{
    public string Value
    {
        get;
    }

    public PermissionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Permission key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
