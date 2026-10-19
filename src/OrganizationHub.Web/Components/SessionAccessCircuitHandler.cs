using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components;

internal sealed class SessionAccessCircuitHandler : CircuitHandler
{
    private readonly AuthenticationStateProvider mAuthenticationStateProvider;
    private readonly NavigationManager mNavigationManager;
    private readonly UserSessionAccessService mAccessService;
    private string mFingerprint = string.Empty;
    private bool mHasRedirected;
    private bool mIsConnected = true;

    public SessionAccessCircuitHandler(AuthenticationStateProvider authenticationStateProvider, NavigationManager navigationManager, UserSessionAccessService accessService)
    {
        mAuthenticationStateProvider = authenticationStateProvider;
        mNavigationManager = navigationManager;
        mAccessService = accessService;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
    {
        return context => handleActivityAsync(context, next);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        mIsConnected = false;
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        mIsConnected = true;
        return Task.CompletedTask;
    }

    public async Task<bool> CheckAccessAsync(CancellationToken cancellationToken)
    {
        if (mHasRedirected || mIsConnected == false)
        {
            return false;
        }

        AuthenticationState authentication = await mAuthenticationStateProvider.GetAuthenticationStateAsync();
        Uri uri = mNavigationManager.ToAbsoluteUri(mNavigationManager.Uri);
        NavigationPath currentPath = NavigationPath.CreateOptional(uri.AbsolutePath);
        NavigationPath validationPath = ComponentRouteMatcher.IsKnownPath(currentPath) ? currentPath : NavigationPath.CreateOptional("/not-found");
        SessionAccessResult result = await mAccessService.ValidateAsync(authentication.User, validationPath, cancellationToken);
        if (mIsConnected == false)
        {
            return false;
        }

        if (result.State != ESessionAccessState.Allowed)
        {
            mHasRedirected = true;
            mNavigationManager.NavigateTo(result.State == ESessionAccessState.SignedOut ? "/login?reason=session-ended" : "/access-denied", forceLoad: true);
            return false;
        }

        if (mFingerprint.Length > 0 && mFingerprint != result.Fingerprint)
        {
            mHasRedirected = true;
            mNavigationManager.NavigateTo(mNavigationManager.Uri, forceLoad: true);
            return false;
        }

        mFingerprint = result.Fingerprint;
        return true;
    }

    private async Task handleActivityAsync(CircuitInboundActivityContext context, Func<CircuitInboundActivityContext, Task> next)
    {
        if (await CheckAccessAsync(CancellationToken.None))
        {
            await next(context);
        }
    }
}
