using System;
using System.Diagnostics;

namespace OrganizationHub.Web.Domain;

public static class StoredCompatibilityRoleUtility
{
    public static string GetClaimRoleName(EStoredCompatibilityRole role)
    {
        switch (role)
        {
            case EStoredCompatibilityRole.Admin:
                return OrganizationHubRoles.ADMIN;
            case EStoredCompatibilityRole.ManagementDirector:
                return OrganizationHubRoles.MANAGEMENT_DIRECTOR;
            case EStoredCompatibilityRole.ManagementDeputyDirector:
                return OrganizationHubRoles.MANAGEMENT_DEPUTY_DIRECTOR;
            case EStoredCompatibilityRole.ManagementManager:
                return OrganizationHubRoles.MANAGEMENT_MANAGER;
            case EStoredCompatibilityRole.ManagementStaff:
                return OrganizationHubRoles.MANAGEMENT_STAFF;
            case EStoredCompatibilityRole.ManagementRecorder:
                return OrganizationHubRoles.MANAGEMENT_RECORDER;
            case EStoredCompatibilityRole.TeamLeader:
                return OrganizationHubRoles.TEAM_LEADER;
            case EStoredCompatibilityRole.TeamDeputyLeader:
                return OrganizationHubRoles.TEAM_DEPUTY_LEADER;
            case EStoredCompatibilityRole.TeamUser:
                return OrganizationHubRoles.TEAM_USER;
            default:
                Debug.Fail("unknown user role");
                return OrganizationHubRoles.TEAM_USER;
        }
    }

    public static string GetDisplayName(EStoredCompatibilityRole role)
    {
        switch (role)
        {
            case EStoredCompatibilityRole.Admin:
                return "시스템 관리자";
            case EStoredCompatibilityRole.ManagementDirector:
                return "조직 책임자";
            case EStoredCompatibilityRole.ManagementDeputyDirector:
                return "조직 부책임자";
            case EStoredCompatibilityRole.ManagementManager:
                return "운영 관리자";
            case EStoredCompatibilityRole.ManagementStaff:
                return "운영 담당자";
            case EStoredCompatibilityRole.ManagementRecorder:
                return "기록 담당자";
            case EStoredCompatibilityRole.TeamLeader:
                return "팀 책임자";
            case EStoredCompatibilityRole.TeamDeputyLeader:
                return "팀 부책임자";
            case EStoredCompatibilityRole.TeamUser:
                return "팀 구성원";
            default:
                Debug.Fail("unknown user role");
                return "팀 구성원";
        }
    }

    public static bool TryParseClaimRoleName(string roleName, out EStoredCompatibilityRole role)
    {
        bool isParsed = Enum.TryParse(roleName, true, out role);
        if (isParsed == false || Enum.IsDefined(role) == false)
        {
            role = EStoredCompatibilityRole.TeamUser;
            return false;
        }

        return true;
    }
}
