using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleRefreshToken
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

    private GoogleRefreshToken(string refreshToken)
    {
        mValueOrNull = refreshToken;
    }

    public static GoogleRefreshToken CreateRequired(string? refreshTokenOrNull)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenOrNull))
        {
            throw new ArgumentException("Google refresh token is required.", nameof(refreshTokenOrNull));
        }

        return new GoogleRefreshToken(refreshTokenOrNull.Trim());
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Google refresh token is required.");
        }

        return Value;
    }

    public override string ToString()
    {
        return "[protected]";
    }
}
