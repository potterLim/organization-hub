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

        string userIDText = getClaimValue(principal, OrganizationHubClaimTypes.USER_ID);
        int userID;
        bool isUserIDParsed = int.TryParse(userIDText, out userID);
        if (isUserIDParsed == false)
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

        int? teamID = null;
        string teamIDText = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_ID);
        if (string.IsNullOrWhiteSpace(teamIDText) == false)
        {
            int parsedTeamID;
            bool isTeamIDParsed = int.TryParse(teamIDText, out parsedTeamID);
            if (isTeamIDParsed)
            {
                teamID = parsedTeamID;
            }
        }

        string username = getClaimValue(principal, OrganizationHubClaimTypes.USERNAME);
        string displayName = getClaimValue(principal, OrganizationHubClaimTypes.DISPLAY_NAME);
        string teamKey = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_KEY);
        string teamDisplayName = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_DISPLAY_NAME);
        return new UserSession(userID, username, displayName, role, teamID, teamKey, teamDisplayName);
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
}

