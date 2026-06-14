using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Domain;

public static partial class UserPermissionPolicy
{
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

    public static bool CanUseOnlyManagementApprovalFolders(EUserRole role)
    {
        return UserRoleUtility.IsManagementWorkingRole(role);
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

                OrganizationTeamId rootTeamId = rootFolder.TeamIdOrNull.GetValueOrDefault();
                bool canDeleteOwnTeamMaterials = userSession.Role == EUserRole.TeamLeader &&
                    userSession.HasTeam &&
                    rootTeamId.HasValue &&
                    userSession.GetRequiredOrganizationTeamId() == rootTeamId;
                if (canDeleteOwnTeamMaterials)
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
}
