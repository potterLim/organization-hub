namespace OrganizationHub.Web.Domain;

public static partial class UserPermissionPolicy
{
    public static bool CanWriteManagementWork(EUserRole role)
    {
        return DecideCalendarWrite(role, ECalendarViewKind.Organization, null, null).IsAllowed;
    }

    public static bool CanWriteResponsibleWork(EUserRole role)
    {
        return DecideCalendarWrite(role, ECalendarViewKind.Responsible, null, null).IsAllowed;
    }

    public static bool CanWriteAnyTeamWork(EUserRole role)
    {
        return UserRoleUtility.IsManagementManagerLevelRole(role);
    }

    public static bool CanWriteOwnTeamWork(EUserRole role)
    {
        return UserRoleUtility.IsTeamRole(role);
    }

    public static PermissionDecision DecideCalendarWrite(
        EUserRole role,
        ECalendarViewKind calendarKind,
        OrganizationTeamId? calendarTeamIdOrNull,
        OrganizationTeamId? userTeamIdOrNull
    )
    {
        switch (calendarKind)
        {
            case ECalendarViewKind.Organization:
                return UserRoleUtility.IsManagementRole(role)
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME)
                    : PermissionDecision.Deny("조직 공용 일정 수정은 조직 관리 역할만 가능합니다.", OrganizationHubWorkspaceDefaults.ORGANIZATION_CALENDAR_NAME);
            case ECalendarViewKind.Responsible:
                return UserRoleUtility.IsManagementManagerLevelRole(role) || role == EUserRole.TeamLeader
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " 일정")
                    : PermissionDecision.Deny("책임자 일정 수정은 팀 책임자 또는 운영 관리자 이상만 가능합니다.", OrganizationHubWorkspaceDefaults.RESPONSIBLE_CALENDAR_NAME + " 일정");
            case ECalendarViewKind.Team:
                if (UserRoleUtility.IsManagementManagerLevelRole(role))
                {
                    return PermissionDecision.Allow("모든 팀 일정");
                }

                if (UserRoleUtility.IsTeamRole(role) && isSameTeam(calendarTeamIdOrNull, userTeamIdOrNull))
                {
                    return PermissionDecision.Allow("자기 팀 일정");
                }

                return PermissionDecision.Deny("팀 일정 수정은 해당 팀 구성원 또는 운영 관리자 이상만 가능합니다.", "팀 일정");
            default:
                return PermissionDecision.Deny("전체 일정은 조회용입니다.", "전체 일정");
        }
    }

    private static bool isSameTeam(OrganizationTeamId? firstTeamIdOrNull, OrganizationTeamId? secondTeamIdOrNull)
    {
        OrganizationTeamId firstTeamId = firstTeamIdOrNull.GetValueOrDefault();
        OrganizationTeamId secondTeamId = secondTeamIdOrNull.GetValueOrDefault();
        if (firstTeamId.HasValue == false || secondTeamId.HasValue == false)
        {
            return false;
        }

        return firstTeamId == secondTeamId;
    }
}
