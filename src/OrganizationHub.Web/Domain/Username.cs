using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct Username
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

    private Username(string usernameValue)
    {
        Value = usernameValue.Trim();
    }

    public static Username CreateOptional(string? usernameOrNull)
    {
        if (string.IsNullOrWhiteSpace(usernameOrNull))
        {
            return new Username(string.Empty);
        }

        return new Username(usernameOrNull);
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Username is required.");
        }

        return Value;
    }

    public string GetOptionalValue()
    {
        if (HasValue == false)
        {
            return string.Empty;
        }

        return Value;
    }
}
