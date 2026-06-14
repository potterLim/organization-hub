using System;
using System.Globalization;

namespace OrganizationHub.Web.Domain;

public readonly record struct GoogleWorkspaceAssignmentTargetKey
{
    private const string OFFICIAL_ROOT_FOLDER_KEY = "OfficialRoot";
    private const string PUBLIC_FOLDER_KEY = "Public";
    private const string COMMON_FOLDER_KEY = "Common";
    private const string RESPONSIBLE_RESOURCE_KEY = "Responsible";
    private const string WORK_RESOURCES_FOLDER_KEY = "WorkResources";
    private const string ORGANIZATION_RESOURCE_KEY = "Organization";
    private const string TEAM_MATERIALS_FOLDER_PREFIX = "TeamMaterials:";
    private const string TEAM_APPROVED_FOLDER_PREFIX = "TeamApproved:";
    private const string TEAM_RESOURCE_PREFIX = "Team:";

    private readonly string? mValueOrNull;

    public string Value
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

    public static GoogleWorkspaceAssignmentTargetKey OfficialRootFolder
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(OFFICIAL_ROOT_FOLDER_KEY);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey Empty
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(string.Empty);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey PublicFolder
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(PUBLIC_FOLDER_KEY);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey CommonFolder
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(COMMON_FOLDER_KEY);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey ResponsibleResource
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(RESPONSIBLE_RESOURCE_KEY);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey WorkResourcesFolder
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(WORK_RESOURCES_FOLDER_KEY);
        }
    }

    public static GoogleWorkspaceAssignmentTargetKey OrganizationResource
    {
        get
        {
            return new GoogleWorkspaceAssignmentTargetKey(ORGANIZATION_RESOURCE_KEY);
        }
    }

    private GoogleWorkspaceAssignmentTargetKey(string targetKey)
    {
        mValueOrNull = normalize(targetKey);
    }

    public static GoogleWorkspaceAssignmentTargetKey FromSelectionValue(string selectionValue)
    {
        return new GoogleWorkspaceAssignmentTargetKey(selectionValue);
    }

    public static GoogleWorkspaceAssignmentTargetKey CreateTeamMaterialsFolder(OrganizationTeamId teamId)
    {
        return createTeamResourceKey(TEAM_MATERIALS_FOLDER_PREFIX, teamId);
    }

    public static GoogleWorkspaceAssignmentTargetKey CreateTeamApprovedFolder(OrganizationTeamId teamId)
    {
        return createTeamResourceKey(TEAM_APPROVED_FOLDER_PREFIX, teamId);
    }

    public static GoogleWorkspaceAssignmentTargetKey CreateTeamResource(OrganizationTeamId teamId)
    {
        return createTeamResourceKey(TEAM_RESOURCE_PREFIX, teamId);
    }

    public bool Matches(GoogleWorkspaceAssignmentTargetKey candidateTargetKey)
    {
        if (HasValue == false)
        {
            return false;
        }

        return string.Equals(GetSelectionValue(), candidateTargetKey.GetSelectionValue(), StringComparison.Ordinal);
    }

    public string GetSelectionValue()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value;
    }

    private static string normalize(string targetKey)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return string.Empty;
        }

        return targetKey.Trim();
    }

    private static GoogleWorkspaceAssignmentTargetKey createTeamResourceKey(string prefix, OrganizationTeamId teamId)
    {
        if (teamId.HasValue == false)
        {
            throw new ArgumentException("Team target key requires a team ID.", nameof(teamId));
        }

        return new GoogleWorkspaceAssignmentTargetKey(prefix + teamId.GetRequiredValue().ToString(CultureInfo.InvariantCulture));
    }
}
