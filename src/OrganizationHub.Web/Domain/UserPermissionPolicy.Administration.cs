namespace OrganizationHub.Web.Domain;

public static partial class UserPermissionPolicy
{
    public static bool CanManageSystemAdministratorAccounts(EUserRole role)
    {
        return DecideManageSystemAdministratorAccounts(role).IsAllowed;
    }

    public static PermissionDecision DecideManageSystemAdministratorAccounts(EUserRole role)
    {
        return role == EUserRole.Admin || role == EUserRole.ManagementDirector
            ? PermissionDecision.Allow("관리자 계정 관리")
            : PermissionDecision.Deny("관리자 계정은 시스템 관리자만 관리할 수 있습니다.", "관리자 계정 관리");
    }

    public static bool CanAssignManagementLeadershipPositions(EUserRole role)
    {
        return DecideAssignManagementLeadershipPositions(role).IsAllowed;
    }

    public static PermissionDecision DecideAssignManagementLeadershipPositions(EUserRole role)
    {
        return role == EUserRole.Admin
            ? PermissionDecision.Allow("관리 직책 역할 지정")
            : PermissionDecision.Deny("관리 직책 역할 지정은 시스템 관리자만 할 수 있습니다.", "관리 직책 역할 지정");
    }
}
