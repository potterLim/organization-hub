using System.Diagnostics;

namespace OrganizationHub.Web.Domain;

public static class MenuAccessLevelUtility
{
    public static string GetDisplayName(EMenuAccessLevel accessLevel)
    {
        switch (accessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return "로그인한 모든 사용자";
            case EMenuAccessLevel.TeamMember:
                return "선택한 팀의 구성원";
            case EMenuAccessLevel.TeamLeader:
                return "선택한 팀의 책임자 이상";
            case EMenuAccessLevel.ManagementManager:
                return "관리팀 책임자 이상";
            case EMenuAccessLevel.Management:
                return "관리팀 전체";
            case EMenuAccessLevel.Admin:
                return "시스템 관리자";
            default:
                Debug.Fail("unknown menu access level");
                return "-";
        }
    }

    public static bool IsTeamScoped(EMenuAccessLevel accessLevel)
    {
        switch (accessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return true;
            case EMenuAccessLevel.TeamMember:
                return true;
            case EMenuAccessLevel.TeamLeader:
                return true;
            case EMenuAccessLevel.ManagementManager:
                return false;
            case EMenuAccessLevel.Management:
                return false;
            case EMenuAccessLevel.Admin:
                return false;
            default:
                Debug.Fail("unknown menu access level");
                return false;
        }
    }

    public static bool IsRoleAllowed(EMenuAccessLevel accessLevel, EUserRole role)
    {
        return UserPermissionPolicy.DecideMenuAccess(accessLevel, role).IsAllowed;
    }
}
