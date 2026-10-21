using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace OrganizationHub.Web.Services;

internal sealed class UserSessionCookieEvents : CookieAuthenticationEvents
{
    private readonly UserSessionAccessService mAccessService;

    public UserSessionCookieEvents(UserSessionAccessService accessService)
    {
        mAccessService = accessService;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal == null)
        {
            context.RejectPrincipal();
            return;
        }

        ClaimsPrincipal? currentOrNull = await mAccessService.GetCurrentPrincipalOrNullAsync(context.Principal, context.HttpContext.RequestAborted);
        if (currentOrNull == null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        context.ReplacePrincipal(currentOrNull);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        string returnPath = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect("/login?returnUrl=" + Uri.EscapeDataString(returnPath));
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.Redirect("/access-denied");
        return Task.CompletedTask;
    }
}
