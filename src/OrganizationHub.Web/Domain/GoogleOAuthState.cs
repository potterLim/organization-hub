using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleOAuthState
{
    public string Value
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
}
