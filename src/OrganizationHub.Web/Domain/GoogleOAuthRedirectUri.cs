using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleOAuthRedirectUri
{
    private string Value
    {
        get;
    }

    public GoogleOAuthRedirectUri(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ArgumentException("Google OAuth redirect URI is required.", nameof(redirectUri));
        }

        string trimmedRedirectUri = redirectUri.Trim();
        Uri? uriOrNull;
        bool isParsed = Uri.TryCreate(trimmedRedirectUri, UriKind.Absolute, out uriOrNull);
        if (isParsed == false || uriOrNull == null)
        {
            throw new ArgumentException("Google OAuth redirect URI must be an absolute URI.", nameof(redirectUri));
        }

        if (uriOrNull.Scheme != Uri.UriSchemeHttp && uriOrNull.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Google OAuth redirect URI must use HTTP or HTTPS.", nameof(redirectUri));
        }

        Value = trimmedRedirectUri;
    }

    public string GetRequiredValue()
    {
        return Value;
    }
}
