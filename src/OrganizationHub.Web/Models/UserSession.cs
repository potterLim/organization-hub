using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UserSession
{
    public UserAccountId UserAccountId
    {
        get; private init;
    }

    public int UserId
    {
        get
        {
            return UserAccountId.GetRequiredValue();
        }
    }

    public string Username { get; private init; } = string.Empty;

    public string DisplayName { get; private init; } = string.Empty;

    public EUserRole Role
    {
        get; private init;
    }

    public OrganizationTeamId? OrganizationTeamIdOrNull
    {
        get; private init;
    }

    public bool HasTeam
    {
        get
        {
            OrganizationTeamId teamId = OrganizationTeamIdOrNull.GetValueOrDefault();
            return teamId.HasValue;
        }
    }

    public int? TeamId
    {
        get
        {
            if (HasTeam == false)
            {
                return null;
            }

            return GetRequiredOrganizationTeamId().GetRequiredValue();
        }
    }

    public OrganizationTeamKey TeamKey
    {
        get; private init;
    }

    public string TeamKeyValue
    {
        get
        {
            return TeamKey.GetOptionalValue();
        }
    }

    public string TeamDisplayName { get; private init; } = string.Empty;

    public UserSession(
        UserAccountId userId,
        string username,
        string displayName,
        EUserRole role,
        OrganizationTeamId? teamIdOrNull,
        OrganizationTeamKey teamKey,
        string teamDisplayName)
    {
        UserAccountId = userId;
        Username = username;
        DisplayName = displayName;
        Role = role;
        OrganizationTeamIdOrNull = teamIdOrNull;
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

    public OrganizationTeamId GetRequiredOrganizationTeamId()
    {
        OrganizationTeamId teamId = OrganizationTeamIdOrNull.GetValueOrDefault();
        if (teamId.HasValue == false)
        {
            throw new InvalidOperationException("User session team is required.");
        }

        return teamId;
    }

    public int GetRequiredTeamIdValue()
    {
        return GetRequiredOrganizationTeamId().GetRequiredValue();
    }
}
