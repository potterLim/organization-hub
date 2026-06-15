using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PlainPassword
{
    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return string.Empty;
            }

            return mValueOrNull;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    private PlainPassword(string plainPassword)
    {
        mValueOrNull = plainPassword;
    }

    public static PlainPassword CreateFromInput(string? passwordOrNull)
    {
        if (passwordOrNull == null)
        {
            return new PlainPassword(string.Empty);
        }

        return new PlainPassword(passwordOrNull);
    }

    public bool HasMinimumLength(int minimumLength)
    {
        return Value.Length >= minimumLength;
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Password is required.");
        }

        return Value;
    }
}
