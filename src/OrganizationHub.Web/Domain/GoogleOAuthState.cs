using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleOAuthState
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

    public GoogleOAuthState(string state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        Value = state.Trim();
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Google OAuth state is required.");
        }

        return Value;
    }
}
