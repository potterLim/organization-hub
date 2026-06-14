using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Controllers;

[Authorize(Policy = OrganizationHubPolicies.GOOGLE_CONNECTION_MANAGE)]
public sealed class GoogleConnectionController : Controller
{
    private readonly IGoogleConnectionService mGoogleConnectionService;

    public GoogleConnectionController(IGoogleConnectionService googleConnectionService)
    {
        mGoogleConnectionService = googleConnectionService;
    }

    [HttpGet("/admin/google/connect")]
    public IActionResult Connect()
    {
        try
        {
            GoogleOAuthRedirectUri redirectUri = createCallbackUri();
            string authorizationUrl = mGoogleConnectionService.CreateAuthorizationUrl(redirectUri);
            return Redirect(authorizationUrl);
        }
        catch (InvalidOperationException exception)
        {
            return Redirect("/admin/google?message=" + Uri.EscapeDataString(exception.Message));
        }
    }

    [ActionName("Callback")]
    [HttpGet("/admin/google/callback")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery(Name = "code")] string? codeOrNull,
        [FromQuery(Name = "state")] string? stateOrNull,
        [FromQuery(Name = "error")] string? errorOrNull
    )
    {
        if (string.IsNullOrWhiteSpace(errorOrNull) == false)
        {
            return Redirect("/admin/google?message=" + Uri.EscapeDataString("Google 연결이 취소되었습니다. " + errorOrNull));
        }

        GoogleOAuthAuthorizationCode authorizationCode = new GoogleOAuthAuthorizationCode(getTextOrEmpty(codeOrNull));
        GoogleOAuthState state = new GoogleOAuthState(getTextOrEmpty(stateOrNull));
        GoogleOAuthRedirectUri redirectUri = createCallbackUri();
        ServiceResult connectionResult = await mGoogleConnectionService.CompleteConnectionAsync(authorizationCode, state, redirectUri, HttpContext.RequestAborted);
        if (connectionResult.IsSuccess == false)
        {
            return Redirect("/admin/google?message=" + Uri.EscapeDataString(connectionResult.ErrorMessage));
        }

        return Redirect("/admin/google?message=" + Uri.EscapeDataString("Google 계정 연결이 완료되었습니다."));
    }

    private GoogleOAuthRedirectUri createCallbackUri()
    {
        string callbackUri = Request.Scheme + "://" + Request.Host.ToUriComponent() + "/admin/google/callback";
        return new GoogleOAuthRedirectUri(callbackUri);
    }

    private string getTextOrEmpty(string? textOrNull)
    {
        if (textOrNull == null)
        {
            return string.Empty;
        }

        return textOrNull;
    }
}
