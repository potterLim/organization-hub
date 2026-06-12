using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class UserPermissionPolicyTests
{
    [Fact]
    public void MenuPolicyKeepsAdministratorOutOfGeneralUserMenus()
    {
        PermissionDecision teamMemberDecision = UserPermissionPolicy.DecideMenuAccess(EMenuAccessLevel.TeamMember, EUserRole.Admin);
        PermissionDecision managementDecision = UserPermissionPolicy.DecideMenuAccess(EMenuAccessLevel.Management, EUserRole.Admin);
        PermissionDecision adminDecision = UserPermissionPolicy.DecideMenuAccess(EMenuAccessLevel.Admin, EUserRole.Admin);

        Assert.False(teamMemberDecision.IsAllowed);
        Assert.False(managementDecision.IsAllowed);
        Assert.True(adminDecision.IsAllowed);
        Assert.Equal("조직 사용자 메뉴", teamMemberDecision.ScopeLabel);
    }

    [Fact]
    public void MaterialPolicyReturnsDecisionReasonAndScope()
    {
        PermissionDecision staffPublicUploadDecision = UserPermissionPolicy.DecideMaterialUpload(EUserRole.ManagementStaff, EDriveFolderKind.Public, ETeamScope.RegularTeam);
        PermissionDecision directorCommonUploadDecision = UserPermissionPolicy.DecideMaterialUpload(EUserRole.ManagementDirector, EDriveFolderKind.Common, ETeamScope.RegularTeam);
        PermissionDecision teamUserTeamUploadDecision = UserPermissionPolicy.DecideMaterialUpload(EUserRole.TeamUser, EDriveFolderKind.TeamMaterials, ETeamScope.RegularTeam);

        Assert.False(staffPublicUploadDecision.IsAllowed);
        Assert.Equal("외부 공유 자료", staffPublicUploadDecision.ScopeLabel);
        Assert.Contains("외부 공유 자료 업로드", staffPublicUploadDecision.Reason);
        Assert.True(directorCommonUploadDecision.IsAllowed);
        Assert.Equal("공통 자료", directorCommonUploadDecision.ScopeLabel);
        Assert.True(teamUserTeamUploadDecision.IsAllowed);
        Assert.Equal("팀 자료", teamUserTeamUploadDecision.ScopeLabel);
    }

    [Fact]
    public void ApprovalPolicyExcludesAdministratorFromRequestAndApproverCandidate()
    {
        PermissionDecision requestDecision = UserPermissionPolicy.DecideCreateApprovalRequest(EUserRole.Admin);
        PermissionDecision finalApproverDecision = UserPermissionPolicy.DecideFinalApprovalApprover(EUserRole.TeamUser, EUserRole.Admin);

        Assert.False(requestDecision.IsAllowed);
        Assert.Equal("결재 요청", requestDecision.ScopeLabel);
        Assert.False(finalApproverDecision.IsAllowed);
        Assert.Equal("팀 최종 결재 후보", finalApproverDecision.ScopeLabel);
    }
}
