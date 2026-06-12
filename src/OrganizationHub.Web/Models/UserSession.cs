using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UserSession
{
    public int UserID
    {
        get; private init;
    }
    public string Username { get; private init; } = string.Empty;
    public string DisplayName { get; private init; } = string.Empty;
    public EUserRole Role
    {
        get; private init;
    }
    public int? TeamID
    {
        get; private init;
    }
    public string TeamKey { get; private init; } = string.Empty;
    public string TeamDisplayName { get; private init; } = string.Empty;

    public UserSession(int userID, string username, string displayName, EUserRole role, int? teamID, string teamKey, string teamDisplayName)
    {
        UserID = userID;
        Username = username;
        DisplayName = displayName;
        Role = role;
        TeamID = teamID;
        TeamKey = teamKey;
        TeamDisplayName = teamDisplayName;
    }

    public bool IsManagementRole()
    {
        return UserRoleUtility.IsManagementRole(Role);
    }

    public bool IsManagementManagerLevelRole()
    {
        return UserRoleUtility.IsManagementManagerLevelRole(Role);
    }

    public bool IsTeamRole()
    {
        return UserRoleUtility.IsTeamRole(Role);
    }

    public bool IsTeamLeaderOrHigherRole()
    {
        return UserRoleUtility.IsTeamLeaderOrHigherRole(Role);
    }
}
