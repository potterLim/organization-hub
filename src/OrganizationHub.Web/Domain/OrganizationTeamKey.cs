using System;

namespace OrganizationHub.Web.Domain;

public readonly record struct OrganizationTeamKey
{
    private readonly string? mValueOrNull;

    private string Value
    {
        get
        {
            if (mValueOrNull == null)
            {
                return string.Empty;
            }

            return mValueOrNull;
        }
    }

    public bool HasValue
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public bool IsManagementTeamKey
    {
        get
        {
            return Matches(Management);
        }
    }

    public bool IsSystemAdministrationTeamKey
    {
        get
        {
            return Matches(SystemAdministration);
        }
    }

    public static OrganizationTeamKey Empty
    {
        get
        {
            return new OrganizationTeamKey(string.Empty);
        }
    }

    public static OrganizationTeamKey Management
    {
        get
        {
            return new OrganizationTeamKey(OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY);
        }
    }

    public static OrganizationTeamKey SystemAdministration
    {
        get
        {
            return new OrganizationTeamKey(OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY);
        }
    }

    public OrganizationTeamKey(string teamKey)
    {
        if (string.IsNullOrWhiteSpace(teamKey))
        {
            mValueOrNull = string.Empty;
            return;
        }

        mValueOrNull = teamKey.Trim();
    }

    public bool Matches(OrganizationTeamKey otherTeamKey)
    {
        return string.Equals(GetOptionalValue(), otherTeamKey.GetOptionalValue(), StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
        return Value;
    }

    public string GetOptionalValue()
    {
        return Value;
    }

    public string GetRequiredValue()
    {
        if (HasValue == false)
        {
            throw new InvalidOperationException("Organization team key is required.");
        }

        return Value;
    }
}
