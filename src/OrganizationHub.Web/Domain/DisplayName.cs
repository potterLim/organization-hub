using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct DisplayName
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

    private DisplayName(string displayName)
    {
        mValueOrNull = displayName;
    }

    public static DisplayName CreateOptional(string? displayNameOrNull)
    {
        if (string.IsNullOrWhiteSpace(displayNameOrNull))
        {
            return new DisplayName(string.Empty);
        }

        return new DisplayName(displayNameOrNull.Trim());
    }

    public static DisplayName CreateRequired(string? displayNameOrNull)
    {
        DisplayName displayName = CreateOptional(displayNameOrNull);
        if (displayName.HasValue == false)
        {
            throw new ArgumentException("Display name is required.", nameof(displayNameOrNull));
        }

        return displayName;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Display name is required.");
        }

        return Value;
    }
}
