using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAuthService mAuthService;

    public AccountController(IAuthService authService)
    {
        mAuthService = authService;
    }

    [AllowAnonymous]
    [ActionName("Login")]
    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginAsync(
        [FromForm(Name = "username")] string? usernameOrNull,
        [FromForm(Name = "password")] string? passwordOrNull,
        [FromForm(Name = "returnUrl")] string? returnUrlOrNull = null
    )
    {
        Username username = Username.CreateOptional(usernameOrNull);
        PlainPassword password = PlainPassword.CreateFromInput(passwordOrNull);
        LoginResult loginResult = await mAuthService.ValidateLoginAsync(username, password, HttpContext.RequestAborted);
        if (loginResult.IsSuccess == false || loginResult.PrincipalOrNull == null)
        {
            string errorMessage = loginResult.ErrorMessage;
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "로그인에 실패했습니다.";
            }

            string returnQuery = returnUrlOrNull != null && Url.IsLocalUrl(returnUrlOrNull) ? "&returnUrl=" + Uri.EscapeDataString(returnUrlOrNull) : string.Empty;
            return Redirect("/login?error=" + Uri.EscapeDataString(errorMessage) + returnQuery);
        }

        AuthenticationProperties authenticationProperties = new AuthenticationProperties();
        authenticationProperties.IsPersistent = true;
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, loginResult.PrincipalOrNull, authenticationProperties);
        if (returnUrlOrNull != null && Url.IsLocalUrl(returnUrlOrNull))
        {
            return LocalRedirect(returnUrlOrNull);
        }
        return Redirect("/dashboard");
    }

    [Authorize]
    [ActionName("Logout")]
    [HttpPost("/account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        await mAuthService.RevokeLoginAsync(User, HttpContext.RequestAborted);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
