using System.Diagnostics;

namespace OrganizationHub.Web.Domain;

public static class MenuAccessLevelUtility
{
    public static string GetDisplayName(EMenuAccessLevel accessLevel)
    {
        switch (accessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return "전체 사용자";
            case EMenuAccessLevel.TeamMember:
                return "선택한 팀";
            case EMenuAccessLevel.TeamLeader:
                return "선택한 팀과 필요 권한";
            case EMenuAccessLevel.ManagementManager:
                return "관리 권한 보유자";
            case EMenuAccessLevel.Management:
                return "관리팀 전체";
            case EMenuAccessLevel.Admin:
                return "시스템 관리자";
            default:
                Debug.Fail("unknown menu access level");
                return "-";
        }
    }

    public static string GetShortDisplayName(EMenuAccessLevel accessLevel)
    {
        switch (accessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return "전체 사용자";
            case EMenuAccessLevel.TeamMember:
                return "선택한 팀";
            case EMenuAccessLevel.TeamLeader:
                return "선택 팀 + 권한";
            case EMenuAccessLevel.ManagementManager:
                return "관리 권한";
            case EMenuAccessLevel.Management:
                return "관리팀";
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

}
