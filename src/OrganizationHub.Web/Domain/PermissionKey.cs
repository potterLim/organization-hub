using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PermissionKey
{
    private string Value
    {
        get;
    }

    public PermissionKey(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            throw new ArgumentException("Permission key is required.", nameof(permissionKey));
        }

        Value = permissionKey.Trim();
    }

    public override string ToString()
    {
        return Value;
    }

    public string GetRequiredValue()
    {
        return Value;
    }
}
