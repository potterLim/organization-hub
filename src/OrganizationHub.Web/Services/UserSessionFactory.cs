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

        string compatibilityRoleText = getClaimValue(principal, OrganizationHubClaimTypes.ROLE);
        EStoredCompatibilityRole storedCompatibilityRole;
        bool isRoleParsed = StoredCompatibilityRoleUtility.TryParseClaimRoleName(compatibilityRoleText, out storedCompatibilityRole);
        if (isRoleParsed == false)
        {
            return null;
        }

        string teamIdText = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_ID);
        string authenticationVersionText = getClaimValue(principal, OrganizationHubClaimTypes.AUTHENTICATION_VERSION);
        int authenticationVersion = 0;
        if (authenticationVersionText.Length > 0 && (int.TryParse(authenticationVersionText, out authenticationVersion) == false || authenticationVersion < 0))
        {
            return null;
        }

        string username = getClaimValue(principal, OrganizationHubClaimTypes.USERNAME);
        string displayName = getClaimValue(principal, OrganizationHubClaimTypes.DISPLAY_NAME);
        string teamKey = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_KEY);
        string teamDisplayName = getClaimValue(principal, OrganizationHubClaimTypes.TEAM_DISPLAY_NAME);
        UserAccountId userAccountId = UserAccountId.CreateExisting(userId);
        OrganizationTeamId? organizationTeamIdOrNull = createOrganizationTeamIdOrNull(teamIdText);
        UserSessionCreateRequest request = new UserSessionCreateRequest
        {
            UserAccountId = userAccountId,
            AuthenticationVersion = authenticationVersion,
            Username = Username.CreateOptional(username),
            DisplayName = DisplayName.CreateOptional(displayName),
            StoredCompatibilityRole = storedCompatibilityRole,
            OrganizationTeamIdOrNull = organizationTeamIdOrNull,
            TeamKey = new OrganizationTeamKey(teamKey),
            TeamDisplayName = DisplayName.CreateOptional(teamDisplayName)
        };
        return new UserSession(request);
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
