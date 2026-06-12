using System;
using System.Diagnostics;

namespace OrganizationHub.Web.Domain;

public static class UserRoleUtility
{
    public static string GetRoleName(EUserRole role)
    {
        switch (role)
        {
            case EUserRole.Admin:
                return OrganizationHubRoles.ADMIN;
            case EUserRole.ManagementDirector:
                return OrganizationHubRoles.MANAGEMENT_DIRECTOR;
            case EUserRole.ManagementDeputyDirector:
                return OrganizationHubRoles.MANAGEMENT_DEPUTY_DIRECTOR;
            case EUserRole.ManagementManager:
                return OrganizationHubRoles.MANAGEMENT_MANAGER;
            case EUserRole.ManagementStaff:
                return OrganizationHubRoles.MANAGEMENT_STAFF;
            case EUserRole.ManagementRecorder:
                return OrganizationHubRoles.MANAGEMENT_RECORDER;
            case EUserRole.TeamLeader:
                return OrganizationHubRoles.TEAM_LEADER;
            case EUserRole.TeamDeputyLeader:
                return OrganizationHubRoles.TEAM_DEPUTY_LEADER;
            case EUserRole.TeamUser:
                return OrganizationHubRoles.TEAM_USER;
            default:
                Debug.Fail("unknown user role");
                return OrganizationHubRoles.TEAM_USER;
        }
    }

    public static string GetDisplayName(EUserRole role)
    {
        switch (role)
        {
            case EUserRole.Admin:
                return "시스템 관리자";
            case EUserRole.ManagementDirector:
                return "조직 책임자";
            case EUserRole.ManagementDeputyDirector:
                return "조직 부책임자";
            case EUserRole.ManagementManager:
                return "운영 관리자";
            case EUserRole.ManagementStaff:
                return "운영 담당자";
            case EUserRole.ManagementRecorder:
                return "기록 담당자";
            case EUserRole.TeamLeader:
                return "팀 책임자";
            case EUserRole.TeamDeputyLeader:
                return "팀 부책임자";
            case EUserRole.TeamUser:
                return "팀 구성원";
            default:
                Debug.Fail("unknown user role");
                return "팀 구성원";
        }
    }

    public static bool TryGetRole(string roleName, out EUserRole role)
    {
        if (string.Equals(roleName, "Management", StringComparison.OrdinalIgnoreCase))
        {
            role = EUserRole.ManagementStaff;
            return true;
        }

        bool isParsed = Enum.TryParse(roleName, true, out role);
        if (isParsed == false)
        {
            role = EUserRole.TeamUser;
            return false;
        }

        return true;
    }

    public static bool IsManagementRole(EUserRole role)
    {
        if (role == EUserRole.Admin)
        {
            return true;
        }

        return IsManagementMemberRole(role);
    }

    public static bool IsManagementMemberRole(EUserRole role)
    {
        return role == EUserRole.ManagementDirector ||
            role == EUserRole.ManagementDeputyDirector ||
            role == EUserRole.ManagementManager ||
            role == EUserRole.ManagementStaff ||
            role == EUserRole.ManagementRecorder;
    }

    public static bool IsManagementWorkingRole(EUserRole role)
    {
        return role == EUserRole.ManagementStaff || role == EUserRole.ManagementRecorder;
    }

    public static bool IsManagementManagerLevelRole(EUserRole role)
    {
        return role == EUserRole.Admin || role == EUserRole.ManagementDirector || role == EUserRole.ManagementDeputyDirector || role == EUserRole.ManagementManager;
    }

    public static bool IsFinalApprovalManagerRole(EUserRole role)
    {
        return role == EUserRole.ManagementManager;
    }

    public static bool IsManagementLeadershipRole(EUserRole role)
    {
        return role == EUserRole.ManagementDirector || role == EUserRole.ManagementDeputyDirector || role == EUserRole.ManagementManager;
    }

    public static bool IsTeamRole(EUserRole role)
    {
        return role == EUserRole.TeamLeader || role == EUserRole.TeamDeputyLeader || role == EUserRole.TeamUser;
    }

    public static bool IsTeamLeaderOrHigherRole(EUserRole role)
    {
        if (IsManagementRole(role))
        {
            return true;
        }

        return role == EUserRole.TeamLeader;
    }

    public static EUserAffiliation GetAffiliation(EUserRole role)
    {
        if (IsManagementRole(role))
        {
            return EUserAffiliation.Management;
        }

        return EUserAffiliation.Team;
    }

    public static EManagementPosition GetManagementPosition(EUserRole role)
    {
        switch (role)
        {
            case EUserRole.ManagementDirector:
                return EManagementPosition.Director;
            case EUserRole.ManagementDeputyDirector:
                return EManagementPosition.DeputyDirector;
            case EUserRole.ManagementManager:
                return EManagementPosition.Manager;
            case EUserRole.ManagementRecorder:
                return EManagementPosition.Recorder;
            default:
                return EManagementPosition.Staff;
        }
    }

    public static ETeamPosition GetTeamPosition(EUserRole role)
    {
        switch (role)
        {
            case EUserRole.TeamLeader:
                return ETeamPosition.Leader;
            case EUserRole.TeamDeputyLeader:
                return ETeamPosition.DeputyLeader;
            default:
                return ETeamPosition.Member;
        }
    }

    public static EUserRole ToManagementRole(EManagementPosition position)
    {
        switch (position)
        {
            case EManagementPosition.Director:
                return EUserRole.ManagementDirector;
            case EManagementPosition.DeputyDirector:
                return EUserRole.ManagementDeputyDirector;
            case EManagementPosition.Manager:
                return EUserRole.ManagementManager;
            case EManagementPosition.Recorder:
                return EUserRole.ManagementRecorder;
            default:
                return EUserRole.ManagementStaff;
        }
    }

    public static EUserRole ToTeamRole(ETeamPosition position)
    {
        switch (position)
        {
            case ETeamPosition.Leader:
                return EUserRole.TeamLeader;
            case ETeamPosition.DeputyLeader:
                return EUserRole.TeamDeputyLeader;
            default:
                return EUserRole.TeamUser;
        }
    }

    public static string GetAffiliationDisplayName(EUserAffiliation affiliation)
    {
        switch (affiliation)
        {
            case EUserAffiliation.Management:
                return OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME;
            default:
                return "팀";
        }
    }

    public static string GetManagementPositionDisplayName(EManagementPosition position)
    {
        return GetDisplayName(ToManagementRole(position));
    }

    public static string GetTeamPositionDisplayName(ETeamPosition position)
    {
        return GetDisplayName(ToTeamRole(position));
    }
}
