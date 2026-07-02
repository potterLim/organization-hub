using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct PlainPassword
{
    public const int MINIMUM_LENGTH = 8;

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

    private bool hasMinimumLength(int minimumLength)
    {
        return Value.Length >= minimumLength;
    }

    public bool HasRequiredMinimumLength()
    {
        return hasMinimumLength(MINIMUM_LENGTH);
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
