namespace OrganizationHub.Web.Domain;

public static partial class UserPermissionPolicy
{
    public static PermissionDecision DecideMenuAccess(EMenuAccessLevel accessLevel, EUserRole role)
    {
        switch (accessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return PermissionDecision.Allow("로그인 사용자 메뉴");
            case EMenuAccessLevel.TeamMember:
                return UserRoleUtility.IsManagementMemberRole(role) || UserRoleUtility.IsTeamRole(role)
                    ? PermissionDecision.Allow("조직 사용자 메뉴")
                    : PermissionDecision.Deny("이 메뉴는 조직 사용자만 접근할 수 있습니다.", "조직 사용자 메뉴");
            case EMenuAccessLevel.TeamLeader:
                return UserRoleUtility.IsManagementMemberRole(role) || role == EUserRole.TeamLeader
                    ? PermissionDecision.Allow("책임자 이상 메뉴")
                    : PermissionDecision.Deny("이 메뉴는 팀 책임자 또는 조직 관리 사용자만 접근할 수 있습니다.", "책임자 이상 메뉴");
            case EMenuAccessLevel.ManagementManager:
                return UserRoleUtility.IsManagementManagerLevelRole(role)
                    ? PermissionDecision.Allow("운영 관리자 메뉴")
                    : PermissionDecision.Deny("이 메뉴는 운영 관리자 또는 시스템 관리자만 접근할 수 있습니다.", "운영 관리자 메뉴");
            case EMenuAccessLevel.Management:
                return UserRoleUtility.IsManagementMemberRole(role)
                    ? PermissionDecision.Allow("조직 관리 메뉴")
                    : PermissionDecision.Deny("이 메뉴는 조직 관리 사용자만 접근할 수 있습니다.", "조직 관리 메뉴");
            case EMenuAccessLevel.Admin:
                return role == EUserRole.Admin
                    ? PermissionDecision.Allow("관리자 메뉴")
                    : PermissionDecision.Deny("이 메뉴는 관리자만 접근할 수 있습니다.", "관리자 메뉴");
            default:
                return PermissionDecision.Deny("알 수 없는 메뉴 권한입니다.", "메뉴");
        }
    }
}
