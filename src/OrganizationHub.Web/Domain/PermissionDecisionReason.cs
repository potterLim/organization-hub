using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PermissionDecisionReason
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

    public static PermissionDecisionReason Empty
    {
        get
        {
            return new PermissionDecisionReason(string.Empty);
        }
    }

    private PermissionDecisionReason(string reason)
    {
        Value = reason;
    }

    public static PermissionDecisionReason CreateRequired(string? reasonOrNull)
    {
        if (string.IsNullOrWhiteSpace(reasonOrNull))
        {
            throw new ArgumentException("Permission decision reason is required.", nameof(reasonOrNull));
        }

        return new PermissionDecisionReason(reasonOrNull.Trim());
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Permission decision reason is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }
}
