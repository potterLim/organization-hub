using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleConnectedEmail
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

    private GoogleConnectedEmail(string connectedEmail)
    {
        mValueOrNull = connectedEmail;
    }

    public static GoogleConnectedEmail CreateRequired(string? connectedEmailOrNull)
    {
        if (string.IsNullOrWhiteSpace(connectedEmailOrNull))
        {
            throw new ArgumentException("Google connected email is required.", nameof(connectedEmailOrNull));
        }

        return new GoogleConnectedEmail(connectedEmailOrNull.Trim());
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Google connected email is required.");
        }

        return Value;
    }
}
