using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OrganizationHub.Web.Models;
using OrganizationHub.Web.Services;

namespace OrganizationHub.Web.Components;

public abstract class OrganizationHubComponentBase : ComponentBase
{
    [Inject]
    protected AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    protected UserSession? CurrentUserSessionOrNull
    {
        get; private set;
    }

    protected async Task<bool> loadUserSessionAsync()
    {
        AuthenticationState authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        ClaimsPrincipal principal = authenticationState.User;
        CurrentUserSessionOrNull = UserSessionFactory.CreateOrNull(principal);
        return CurrentUserSessionOrNull != null;
    }
}

