using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class UserSessionAccessService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IAuthService mAuthService;
    private readonly INavigationMenuService mNavigationMenuService;

    public UserSessionAccessService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IAuthService authService, INavigationMenuService navigationMenuService)
    {
        mDbContextFactory = dbContextFactory;
        mAuthService = authService;
        mNavigationMenuService = navigationMenuService;
    }

    public async Task<ClaimsPrincipal?> GetCurrentPrincipalOrNullAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        UserAccount? userOrNull = await loadActiveUserOrNullAsync(principal, cancellationToken);
        if (userOrNull == null)
        {
            return null;
        }

        ClaimsPrincipal current = mAuthService.CreateClaimsPrincipal(userOrNull);
        Claim? loginClaimOrNull = principal.FindFirst(OrganizationHubClaimTypes.LOGIN_SESSION_ID);
        if (loginClaimOrNull != null)
        {
            ((ClaimsIdentity)current.Identity!).AddClaim(loginClaimOrNull);
        }
        return current;
    }

    public async Task<SessionAccessResult> ValidateAsync(ClaimsPrincipal principal, NavigationPath path, CancellationToken cancellationToken)
    {
        if (principal.Identity == null || principal.Identity.IsAuthenticated == false)
        {
            return new SessionAccessResult { State = ESessionAccessState.Allowed, Fingerprint = string.Empty };
        }

        UserAccount? userOrNull = await loadActiveUserOrNullAsync(principal, cancellationToken);
        if (userOrNull == null)
        {
            return new SessionAccessResult { State = ESessionAccessState.SignedOut, Fingerprint = string.Empty };
        }

        ClaimsPrincipal currentPrincipal = mAuthService.CreateClaimsPrincipal(userOrNull);
        UserSession session = UserSessionFactory.CreateOrNull(currentPrincipal)!;
        bool canAccess = await mNavigationMenuService.CanAccessPathAsync(session, path, cancellationToken);
        string snapshot = JsonSerializer.Serialize(new
        {
            userOrNull.Username,
            userOrNull.DisplayName,
            userOrNull.TeamId,
            userOrNull.PositionId,
            userOrNull.Position!.IsSystemAdministrator,
            Grants = userOrNull.Position.PermissionGrants.OrderBy(grant => grant.Id).Select(grant => new { grant.PermissionKey, grant.IsAllowed, grant.ScopeType, grant.TargetTeamId })
        });
        return new SessionAccessResult
        {
            State = canAccess ? ESessionAccessState.Allowed : ESessionAccessState.Forbidden,
            Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)))
        };
    }

    private async Task<UserAccount?> loadActiveUserOrNullAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        UserSession? sessionOrNull = UserSessionFactory.CreateOrNull(principal);
        if (sessionOrNull == null)
        {
            return null;
        }

        int userId = sessionOrNull.UserAccountId.GetRequiredValue();
        int authenticationVersion = sessionOrNull.AuthenticationVersion;
        await using (OrganizationHubDbContext db = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string? loginIdOrNull = principal.FindFirstValue(OrganizationHubClaimTypes.LOGIN_SESSION_ID);
            if (string.IsNullOrEmpty(loginIdOrNull) == false && await db.RevokedLoginSessions.AnyAsync(item => item.SessionId == loginIdOrNull, cancellationToken))
            {
                return null;
            }
            return await db.UserAccounts.AsNoTracking().Include(user => user.Team).Include(user => user.Position).ThenInclude(position => position!.PermissionGrants)
                .FirstOrDefaultAsync(user => user.Id == userId && user.AuthenticationVersion == authenticationVersion && user.IsActive && user.Position != null && user.Position.IsActive &&
                    (user.TeamId == null || (user.Team != null && user.Team.IsActive)), cancellationToken);
        }
    }
}
