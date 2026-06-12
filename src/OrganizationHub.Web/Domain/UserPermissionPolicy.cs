using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Domain;

public static class UserPermissionPolicy
{
    public static bool CanCreateApprovalRequests(EUserRole role)
    {
        return DecideCreateApprovalRequest(role).IsAllowed;
    }

    public static PermissionDecision DecideCreateApprovalRequest(EUserRole role)
    {
        if (role == EUserRole.Admin)
        {
            return PermissionDecision.Deny("관리자 계정은 결재 요청자가 아닙니다.", "결재 요청");
        }

        return PermissionDecision.Allow("결재 요청");
    }

    public static bool UsesDefaultFinalApproval(EUserRole requesterRole)
    {
        return UserRoleUtility.IsManagementMemberRole(requesterRole) == false;
    }

    public static bool CanUseAsIntermediateApprovalApprover(EUserRole requesterRole, EUserRole candidateRole)
    {
        return DecideIntermediateApprovalApprover(requesterRole, candidateRole).IsAllowed;
    }

    public static PermissionDecision DecideIntermediateApprovalApprover(EUserRole requesterRole, EUserRole candidateRole)
    {
        if (UserRoleUtility.IsManagementMemberRole(requesterRole))
        {
            return isManagementApprovalCandidate(candidateRole)
                ? PermissionDecision.Allow("조직 관리 결재 후보")
                : PermissionDecision.Deny("조직 관리 결재선 후보가 아닌 역할입니다.", "조직 관리 결재 후보");
        }

        bool isAllowed = candidateRole == EUserRole.TeamLeader || UserRoleUtility.IsManagementWorkingRole(candidateRole);
        return isAllowed
            ? PermissionDecision.Allow("팀 결재 1차/2차 후보")
            : PermissionDecision.Deny("팀 결재 중간 결재권자는 팀 책임자 또는 운영 담당자만 선택할 수 있습니다.", "팀 결재 1차/2차 후보");
    }

    public static bool CanUseAsFinalApprovalApprover(EUserRole requesterRole, EUserRole candidateRole)
    {
        return DecideFinalApprovalApprover(requesterRole, candidateRole).IsAllowed;
    }

    public static PermissionDecision DecideFinalApprovalApprover(EUserRole requesterRole, EUserRole candidateRole)
    {
        if (UserRoleUtility.IsManagementMemberRole(requesterRole))
        {
            return isManagementApprovalCandidate(candidateRole)
                ? PermissionDecision.Allow("조직 관리 최종 결재 후보")
                : PermissionDecision.Deny("조직 관리 최종 결재 후보가 아닌 역할입니다.", "조직 관리 최종 결재 후보");
        }

        return candidateRole == EUserRole.ManagementManager
            ? PermissionDecision.Allow("팀 최종 결재 후보")
            : PermissionDecision.Deny("팀 결재의 최종 결재권자는 운영 관리자입니다.", "팀 최종 결재 후보");
    }

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

    public static bool CanUploadPublicMaterials(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.Public, ETeamScope.RegularTeam).IsAllowed;
    }

    public static bool CanUploadCommonMaterials(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.Common, ETeamScope.RegularTeam).IsAllowed;
    }

    public static bool CanUploadWorkResources(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.WorkResources, ETeamScope.RegularTeam).IsAllowed;
    }

    public static bool CanUploadManagementMaterials(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.TeamMaterials, ETeamScope.ManagementTeam).IsAllowed;
    }

    public static bool CanUploadResponsibleResources(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.ResponsibleResources, ETeamScope.RegularTeam).IsAllowed;
    }

    public static bool CanUploadTeamMaterials(EUserRole role)
    {
        return DecideMaterialUpload(role, EDriveFolderKind.TeamMaterials, ETeamScope.RegularTeam).IsAllowed;
    }

    public static PermissionDecision DecideMaterialUpload(EUserRole role, EDriveFolderKind folderKind, ETeamScope teamScope)
    {
        switch (folderKind)
        {
            case EDriveFolderKind.Public:
                return UserRoleUtility.IsManagementManagerLevelRole(role)
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME)
                    : PermissionDecision.Deny("외부 공유 자료 업로드는 운영 관리자 또는 시스템 관리자만 가능합니다.", OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME);
            case EDriveFolderKind.Common:
                return UserRoleUtility.IsManagementRole(role)
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME)
                    : PermissionDecision.Deny("공통 자료 업로드는 조직 관리 역할만 가능합니다.", OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME);
            case EDriveFolderKind.WorkResources:
                return UserRoleUtility.IsManagementRole(role)
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME)
                    : PermissionDecision.Deny("업무 지원 자료 업로드는 조직 관리 역할만 가능합니다.", OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME);
            case EDriveFolderKind.ResponsibleResources:
                return UserRoleUtility.IsManagementManagerLevelRole(role) || role == EUserRole.TeamLeader
                    ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME)
                    : PermissionDecision.Deny("책임자 자료 업로드는 팀 책임자 또는 운영 관리자 이상만 가능합니다.", OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME);
            case EDriveFolderKind.TeamMaterials:
                if (teamScope == ETeamScope.ManagementTeam)
                {
                    return UserRoleUtility.IsManagementRole(role)
                        ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME)
                        : PermissionDecision.Deny("관리팀 자료 업로드는 관리팀 권한이 필요합니다.", OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME);
                }

                return UserRoleUtility.IsManagementManagerLevelRole(role) || UserRoleUtility.IsTeamRole(role)
                    ? PermissionDecision.Allow("팀 자료")
                    : PermissionDecision.Deny("팀 자료 업로드는 팀 구성원 또는 운영 관리자 이상만 가능합니다.", "팀 자료");
            default:
                return PermissionDecision.Deny("이 자료 위치에는 업로드할 수 없습니다.", "자료");
        }
    }

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

    public static bool CanUseOnlyManagementApprovalFolders(EUserRole role)
    {
        return UserRoleUtility.IsManagementWorkingRole(role);
    }

    public static PermissionDecision DecideMaterialDelete(UserSession userSession, DriveFolderReference rootFolder)
    {
        if (userSession.Role == EUserRole.Admin)
        {
            return PermissionDecision.Allow("전체 자료 삭제");
        }

        switch (rootFolder.FolderKind)
        {
            case EDriveFolderKind.TeamMaterials:
                if (rootFolder.IsManagementTeam)
                {
                    return UserRoleUtility.IsManagementManagerLevelRole(userSession.Role)
                        ? PermissionDecision.Allow(OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME)
                        : PermissionDecision.Deny("관리팀 자료 삭제는 운영 관리자 또는 시스템 관리자만 가능합니다.", OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME);
                }

                if (UserRoleUtility.IsManagementManagerLevelRole(userSession.Role))
                {
                    return PermissionDecision.Allow("모든 팀 자료");
                }

                if (userSession.Role == EUserRole.TeamLeader && userSession.TeamID.HasValue && rootFolder.TeamID.HasValue && userSession.TeamID.Value == rootFolder.TeamID.Value)
                {
                    return PermissionDecision.Allow("자기 팀 자료");
                }

                return PermissionDecision.Deny("팀 자료 삭제는 해당 팀 책임자 또는 운영 관리자 이상만 가능합니다.", "팀 자료");
            case EDriveFolderKind.Public:
            case EDriveFolderKind.Common:
            case EDriveFolderKind.WorkResources:
            case EDriveFolderKind.ResponsibleResources:
                return UserRoleUtility.IsManagementManagerLevelRole(userSession.Role)
                    ? PermissionDecision.Allow(rootFolder.DisplayName)
                    : PermissionDecision.Deny(rootFolder.DisplayName + " 삭제는 운영 관리자 또는 시스템 관리자만 가능합니다.", rootFolder.DisplayName);
            case EDriveFolderKind.TeamApproved:
                return PermissionDecision.Deny("결재 완료 자료 삭제는 관리자만 가능합니다.", rootFolder.DisplayName);
            default:
                return PermissionDecision.Deny("이 자료 위치에서는 삭제할 수 없습니다.", rootFolder.DisplayName);
        }
    }

    public static PermissionDecision DecideCalendarWrite(EUserRole role, ECalendarViewKind calendarKind, int? calendarTeamID, int? userTeamID)
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

                if (UserRoleUtility.IsTeamRole(role) && calendarTeamID.HasValue && userTeamID.HasValue && calendarTeamID.Value == userTeamID.Value)
                {
                    return PermissionDecision.Allow("자기 팀 일정");
                }

                return PermissionDecision.Deny("팀 일정 수정은 해당 팀 구성원 또는 운영 관리자 이상만 가능합니다.", "팀 일정");
            default:
                return PermissionDecision.Deny("전체 일정은 조회용입니다.", "전체 일정");
        }
    }

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

    private static bool isManagementApprovalCandidate(EUserRole role)
    {
        return role == EUserRole.ManagementDirector ||
            role == EUserRole.ManagementDeputyDirector ||
            role == EUserRole.ManagementManager ||
            role == EUserRole.TeamLeader ||
            role == EUserRole.ManagementStaff ||
            role == EUserRole.ManagementRecorder;
    }
}
