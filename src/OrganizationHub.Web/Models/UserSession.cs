using System;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Models;

public sealed class UserSession
{
    private readonly Username mUsername;
    private readonly DisplayName mDisplayName;
    private readonly DisplayName mTeamDisplayName;

    public UserAccountId UserAccountId
    {
        get; private init;
    }

    public int AuthenticationVersion
    {
        get; private init;
    }

    public string Username
    {
        get
        {
            return mUsername.GetOptionalValue();
        }
    }

    public string DisplayName
    {
        get
        {
            return mDisplayName.GetOptionalValue();
        }
    }

    public EStoredCompatibilityRole StoredCompatibilityRole
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

    public string TeamDisplayName
    {
        get
        {
            return mTeamDisplayName.GetOptionalValue();
        }
    }

    internal UserSession(UserSessionCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        UserAccountId = request.UserAccountId;
        AuthenticationVersion = request.AuthenticationVersion;
        mUsername = request.Username;
        mDisplayName = request.DisplayName;
        StoredCompatibilityRole = request.StoredCompatibilityRole;
        OrganizationTeamIdOrNull = request.OrganizationTeamIdOrNull;
        TeamKey = request.TeamKey;
        mTeamDisplayName = request.TeamDisplayName;
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
}
