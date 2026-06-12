using System.Security.Claims;

namespace OrganizationHub.Web.Models;

public sealed class LoginResult
{
    public bool IsSuccess
    {
        get; private init;
    }
    public string ErrorMessage { get; private init; } = string.Empty;
    public ClaimsPrincipal? PrincipalOrNull
    {
        get; private init;
    }

    private LoginResult()
    {
    }

    public static LoginResult Succeed(ClaimsPrincipal principal)
    {
        return new LoginResult
        {
            IsSuccess = true,
            PrincipalOrNull = principal
        };
    }

    public static LoginResult Fail(string errorMessage)
    {
        return new LoginResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

