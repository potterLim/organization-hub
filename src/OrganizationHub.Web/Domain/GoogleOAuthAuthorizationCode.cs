using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleOAuthAuthorizationCode
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

    public GoogleOAuthAuthorizationCode(string authorizationCode)
    {
        if (authorizationCode == null)
        {
            throw new ArgumentNullException(nameof(authorizationCode));
        }

        Value = authorizationCode.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Google OAuth authorization code is required.");
        }

        return Value;
    }
}
