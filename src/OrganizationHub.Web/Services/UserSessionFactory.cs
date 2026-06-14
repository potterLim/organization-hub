using System.Security.Claims;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public static class UserSessionFactory
{
    public static UserSession? CreateOrNull(ClaimsPrincipal principal)
    {
        if (principal.Identity == null || principal.Identity.IsAuthenticated == false)
        {
            return null;
        }

        string userIdText = getClaimValue(principal, OrganizationHubClaimTypes.USER_ID);
        int userId;
        bool isUserIdParsed = int.TryParse(userIdText, out userId);
        if (isUserIdParsed == false || userId <= 0)
        {
            return null;
        }

        string roleText = getClaimValue(principal, OrganizationHubClaimTypes.ROLE);
        EUserRole role;
        bool isRoleParsed = UserRoleUtility.TryGetRole(roleText, out role);
        if (isRoleParsed == false)
        {
            return null;
        }

        string teamIdText = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_ID);
        string username = getClaimValue(principal, OrganizationHubClaimTypes.USERNAME);
        string displayName = getClaimValue(principal, OrganizationHubClaimTypes.DISPLAY_NAME);
        string teamKey = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_KEY);
        string teamDisplayName = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_DISPLAY_NAME);
        UserAccountId userAccountId = UserAccountId.CreateExisting(userId);
        OrganizationTeamId? organizationTeamIdOrNull = createOrganizationTeamIdOrNull(teamIdText);
        return new UserSession(userAccountId, username, displayName, role, organizationTeamIdOrNull, new OrganizationTeamKey(teamKey), teamDisplayName);
    }

    private static string getClaimValue(ClaimsPrincipal principal, string claimType)
    {
        Claim? claimOrNull = principal.FindFirst(claimType);
        if (claimOrNull == null)
        {
            return string.Empty;
        }

        return claimOrNull.Value;
    }

    private static OrganizationTeamId? createOrganizationTeamIdOrNull(string teamIdText)
    {
        if (string.IsNullOrWhiteSpace(teamIdText))
        {
            return null;
        }

        int teamId;
        bool isTeamIdParsed = int.TryParse(teamIdText, out teamId);
        if (isTeamIdParsed == false || teamId <= 0)
        {
            return null;
        }

        return OrganizationTeamId.CreateExisting(teamId);
    }
}
