using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UserManagementService
{
    private async Task<ServiceResult> validateUserFormAsync(UserAccountFormModel formModel, EPasswordRequirement passwordRequirement, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.Username))
        {
            return ServiceResult.Fail("아이디를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("표시 이름을 입력해 주세요.");
        }

        if (passwordRequirement == EPasswordRequirement.Required && formModel.Password.Length < 8)
        {
            return ServiceResult.Fail("초기 비밀번호는 8자 이상이어야 합니다.");
        }

        if (formModel.Role != EUserRole.Admin && formModel.TeamID.HasValue == false)
        {
            return ServiceResult.Fail("일반 사용자 계정은 소속 팀이 필요합니다.");
        }

        if (formModel.TeamID.HasValue)
        {
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == formModel.TeamID.Value && team.IsActive, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("활성 팀을 선택해 주세요.");
                }

                if (formModel.Role != EUserRole.Admin && isSystemAdministrationTeam(teamOrNull))
                {
                    return ServiceResult.Fail("시스템 관리 팀은 시스템 관리자 계정에만 사용할 수 있습니다.");
                }

                ServiceResult positionValidationResult = await validatePositionSelectionAsync(dbContext, formModel, cancellationToken);
                if (positionValidationResult.IsSuccess == false)
                {
                    return positionValidationResult;
                }
            }
        }
        else if (formModel.PositionID.HasValue)
        {
            return ServiceResult.Fail("직책을 선택하려면 소속 팀이 필요합니다.");
        }

        return ServiceResult.Succeed();
    }

    private async Task<ServiceResult> validatePositionSelectionAsync(OrganizationHubDbContext dbContext, UserAccountFormModel formModel, CancellationToken cancellationToken)
    {
        if (formModel.PositionID.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        OrganizationPosition? positionOrNull = await dbContext.Positions.FirstOrDefaultAsync(position => position.ID == formModel.PositionID.Value && position.IsActive, cancellationToken);
        if (positionOrNull == null)
        {
            return ServiceResult.Fail("활성 직책을 선택해 주세요.");
        }

        if (formModel.TeamID.HasValue && positionOrNull.TeamID != formModel.TeamID.Value)
        {
            return ServiceResult.Fail("선택한 직책이 소속 팀과 맞지 않습니다.");
        }

        if (formModel.Role != EUserRole.Admin && positionOrNull.IsSystemAdministrator)
        {
            return ServiceResult.Fail("시스템 관리자 직책은 관리자 계정에만 배정할 수 있습니다.");
        }

        return ServiceResult.Succeed();
    }

    private ServiceResult validateAdministratorRoleChange(UserSession userSession, EUserRole role)
    {
        if (role != EUserRole.Admin)
        {
            return ServiceResult.Succeed();
        }

        if (UserPermissionPolicy.CanManageSystemAdministratorAccounts(userSession.Role))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("관리자 계정은 시스템 관리자만 관리할 수 있습니다.");
    }

    private ServiceResult validateManagementLeadershipRoleChange(UserSession userSession, EUserRole role)
    {
        if (UserRoleUtility.IsManagementLeadershipRole(role) == false)
        {
            return ServiceResult.Succeed();
        }

        if (UserPermissionPolicy.CanAssignManagementLeadershipPositions(userSession.Role))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("관리 직책 역할은 시스템 관리자만 관리할 수 있습니다.");
    }

    private EUserRole getAllowedRole(UserAccountFormModel formModel)
    {
        if (formModel.Role == EUserRole.Admin)
        {
            return EUserRole.Admin;
        }

        if (UserRoleUtility.IsManagementMemberRole(formModel.Role))
        {
            return DEFAULT_ORGANIZATION_MEMBER_ROLE;
        }

        return formModel.Role;
    }
}
