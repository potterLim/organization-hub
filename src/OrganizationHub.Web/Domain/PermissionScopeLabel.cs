using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PermissionScopeLabel
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

    private PermissionScopeLabel(string scopeLabel)
    {
        Value = scopeLabel;
    }

    public static PermissionScopeLabel CreateRequired(string? scopeLabelOrNull)
    {
        if (string.IsNullOrWhiteSpace(scopeLabelOrNull))
        {
            throw new ArgumentException("Permission scope label is required.", nameof(scopeLabelOrNull));
        }

        return new PermissionScopeLabel(scopeLabelOrNull.Trim());
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Permission scope label is required.");
        }

        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
