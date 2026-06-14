namespace OrganizationHub.Web.Domain;

public static partial class UserPermissionPolicy
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
