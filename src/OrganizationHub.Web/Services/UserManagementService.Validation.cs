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
    private async Task<ServiceResult> validateUserFormAsync(
        UserAccountFormModel formModel,
        EPasswordRequirement passwordRequirement,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(formModel.Username))
        {
            return ServiceResult.Fail("아이디를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("표시 이름을 입력해 주세요.");
        }

        PlainPassword plainPassword = PlainPassword.CreateFromInput(formModel.Password);
        if (passwordRequirement == EPasswordRequirement.Required && plainPassword.HasRequiredMinimumLength() == false)
        {
            return ServiceResult.Fail("초기 비밀번호는 8자 이상이어야 합니다.");
        }

        OrganizationTeamId? teamIdOrNull = formModel.TeamIdOrNull;
        OrganizationPositionId? positionIdOrNull = formModel.PositionIdOrNull;
        if (formModel.IsSystemAdministrator == false && teamIdOrNull.HasValue == false)
        {
            return ServiceResult.Fail("일반 사용자 계정은 소속 팀이 필요합니다.");
        }

        if (teamIdOrNull.HasValue)
        {
            int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
            if (teamIdValueOrNull.HasValue == false)
            {
                return ServiceResult.Fail("활성 팀을 선택해 주세요.");
            }

            int requiredTeamId = teamIdValueOrNull.GetValueOrDefault();
            await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                Team? teamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team => team.Id == requiredTeamId && team.IsActive, cancellationToken);
                if (teamOrNull == null)
                {
                    return ServiceResult.Fail("활성 팀을 선택해 주세요.");
                }

                if (formModel.IsSystemAdministrator == false && isSystemAdministrationTeam(teamOrNull))
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
        else if (positionIdOrNull.HasValue)
        {
            return ServiceResult.Fail("직책을 선택하려면 소속 팀이 필요합니다.");
        }

        return ServiceResult.Succeed();
    }

    private async Task<ServiceResult> validatePositionSelectionAsync(
        OrganizationHubDbContext dbContext,
        UserAccountFormModel formModel,
        CancellationToken cancellationToken
    )
    {
        OrganizationPositionId? positionIdOrNull = formModel.PositionIdOrNull;
        int? positionIdValueOrNull = OrganizationPositionId.GetValueOrNull(positionIdOrNull);
        if (positionIdValueOrNull.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        OrganizationPosition? positionOrNull = await dbContext.Positions.FirstOrDefaultAsync(
            position => position.Id == positionIdValueOrNull.GetValueOrDefault() && position.IsActive,
            cancellationToken);
        if (positionOrNull == null)
        {
            return ServiceResult.Fail("활성 직책을 선택해 주세요.");
        }

        int? teamIdValueOrNull = OrganizationTeamId.GetValueOrNull(formModel.TeamIdOrNull);
        if (teamIdValueOrNull.HasValue && positionOrNull.TeamId != teamIdValueOrNull.GetValueOrDefault())
        {
            return ServiceResult.Fail("선택한 직책이 소속 팀과 맞지 않습니다.");
        }

        if (formModel.IsSystemAdministrator == false && positionOrNull.IsSystemAdministrator)
        {
            return ServiceResult.Fail("시스템 관리자 직책은 관리자 계정에만 배정할 수 있습니다.");
        }

        return ServiceResult.Succeed();
    }

    private async Task<ServiceResult> validateSystemAdministratorAccountChangeAsync(
        UserSession userSession,
        UserAccount userAccount,
        CancellationToken cancellationToken)
    {
        return await validateSystemAdministratorAccountChangeAsync(userSession, userAccount.Position, cancellationToken);
    }

    private async Task<ServiceResult> validateSystemAdministratorAccountChangeAsync(
        UserSession userSession,
        OrganizationPosition? positionOrNull,
        CancellationToken cancellationToken)
    {
        if (isSystemAdministratorPosition(positionOrNull) == false)
        {
            return ServiceResult.Succeed();
        }

        return await validateRequiredSystemAdministratorAccountChangeAsync(userSession, cancellationToken);
    }

    private async Task<ServiceResult> validateRequestedSystemAdministratorAccountChangeAsync(
        UserSession userSession,
        UserAccountFormModel formModel,
        CancellationToken cancellationToken)
    {
        if (formModel.IsSystemAdministrator)
        {
            return await validateRequiredSystemAdministratorAccountChangeAsync(userSession, cancellationToken);
        }

        OrganizationPositionId? positionIdOrNull = formModel.PositionIdOrNull;
        int? positionIdValueOrNull = OrganizationPositionId.GetValueOrNull(positionIdOrNull);
        if (positionIdValueOrNull.HasValue == false)
        {
            return ServiceResult.Succeed();
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            OrganizationPosition? positionOrNull = await dbContext.Positions.FirstOrDefaultAsync(
                position => position.Id == positionIdValueOrNull.GetValueOrDefault(),
                cancellationToken);
            return await validateSystemAdministratorAccountChangeAsync(userSession, positionOrNull, cancellationToken);
        }
    }

    private async Task<ServiceResult> validateRequiredSystemAdministratorAccountChangeAsync(
        UserSession userSession,
        CancellationToken cancellationToken)
    {
        if (await isSystemAdministratorAsync(userSession, cancellationToken))
        {
            return ServiceResult.Succeed();
        }

        return ServiceResult.Fail("관리자 계정은 시스템 관리자만 관리할 수 있습니다.");
    }

    private bool isSystemAdministratorPosition(OrganizationPosition? positionOrNull)
    {
        return positionOrNull != null && positionOrNull.IsActive && positionOrNull.IsSystemAdministrator;
    }

    private EStoredCompatibilityRole createStoredCompatibilityRole(EUserAccountKind accountKind)
    {
        if (accountKind == EUserAccountKind.SystemAdministrator)
        {
            return EStoredCompatibilityRole.Admin;
        }

        return STANDARD_USER_STORED_COMPATIBILITY_ROLE;
    }

    private async Task<bool> isSystemAdministratorAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int currentUserId = userSession.UserAccountId.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts
                .Include(userAccount => userAccount.Position)
                .FirstOrDefaultAsync(
                    userAccount =>
                        userAccount.Id == currentUserId &&
                        userAccount.AuthenticationVersion == userSession.AuthenticationVersion &&
                        userAccount.IsActive,
                    cancellationToken);
            return userAccountOrNull?.Position != null &&
                userAccountOrNull.Position.IsActive &&
                userAccountOrNull.Position.IsSystemAdministrator;
        }
    }
}
