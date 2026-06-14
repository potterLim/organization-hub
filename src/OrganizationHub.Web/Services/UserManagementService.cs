using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UserManagementService : IUserManagementService
{
    private const EUserRole DEFAULT_ORGANIZATION_MEMBER_ROLE = EUserRole.TeamUser;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPasswordHashService mPasswordHashService;

    public UserManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPasswordHashService passwordHashService)
    {
        mDbContextFactory = dbContextFactory;
        mPasswordHashService = passwordHashService;
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UserAccount> query = dbContext.UserAccounts.Include(user => user.Team).Include(user => user.Position);
            if (UserPermissionPolicy.CanManageSystemAdministratorAccounts(userSession.Role) == false)
            {
                query = query.Where(user => user.Role != EUserRole.Admin);
            }

            List<UserAccount> users = await query.OrderBy(user => user.Username).ToListAsync(cancellationToken);
            return users;
        }
    }

    public async Task<ServiceResult> CreateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult administratorValidationResult = validateAdministratorRoleChange(userSession, formModel.Role);
        if (administratorValidationResult.IsSuccess == false)
        {
            return administratorValidationResult;
        }

        ServiceResult validationResult = await validateUserFormAsync(formModel, EPasswordRequirement.Required, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            string username = formModel.Username.Trim();
            bool hasDuplicateUsername = await dbContext.UserAccounts.AnyAsync(user => user.Username == username, cancellationToken);
            if (hasDuplicateUsername)
            {
                return ServiceResult.Fail("이미 사용 중인 아이디입니다.");
            }

            EUserRole role = getAllowedRole(formModel);
            OrganizationTeamId? teamIdOrNull = await getAllowedTeamIdOrNullAsync(dbContext, formModel, role, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, role, teamIdOrNull, cancellationToken);
            UserAccount userAccount = new UserAccount();
            userAccount.Username = username;
            userAccount.DisplayName = formModel.DisplayName.Trim();
            PasswordHash passwordHash = mPasswordHashService.HashPassword(PlainPassword.CreateFromInput(formModel.Password));
            userAccount.PasswordHash = passwordHash.GetRequiredValue();
            userAccount.Role = role;
            userAccount.TeamId = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
            applyUserPosition(userAccount, positionOrNull);
            userAccount.IsActive = formModel.IsActive;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> UpdateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult administratorValidationResult = validateAdministratorRoleChange(userSession, formModel.Role);
        if (administratorValidationResult.IsSuccess == false)
        {
            return administratorValidationResult;
        }

        ServiceResult validationResult = await validateUserFormAsync(formModel, EPasswordRequirement.Optional, cancellationToken);
        if (validationResult.IsSuccess == false)
        {
            return validationResult;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccountId userAccountId = formModel.UserAccountIdValue;
            int requiredUserAccountId = userAccountId.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            ServiceResult existingAdministratorValidationResult = validateAdministratorRoleChange(userSession, userAccountOrNull.Role);
            if (existingAdministratorValidationResult.IsSuccess == false)
            {
                return existingAdministratorValidationResult;
            }

            ServiceResult existingManagementLeadershipValidationResult = validateManagementLeadershipRoleChange(userSession, userAccountOrNull.Role);
            if (existingManagementLeadershipValidationResult.IsSuccess == false)
            {
                return existingManagementLeadershipValidationResult;
            }

            string username = formModel.Username.Trim();
            bool hasDuplicateUsername = await dbContext.UserAccounts.AnyAsync(user => user.Id != requiredUserAccountId && user.Username == username, cancellationToken);
            if (hasDuplicateUsername)
            {
                return ServiceResult.Fail("이미 사용 중인 아이디입니다.");
            }

            userAccountOrNull.Username = username;
            userAccountOrNull.DisplayName = formModel.DisplayName.Trim();
            EUserRole role = getAllowedRole(formModel);
            OrganizationTeamId? teamIdOrNull = await getAllowedTeamIdOrNullAsync(dbContext, formModel, role, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, role, teamIdOrNull, cancellationToken);
            userAccountOrNull.Role = role;
            userAccountOrNull.TeamId = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
            applyUserPosition(userAccountOrNull, positionOrNull);
            userAccountOrNull.IsActive = formModel.IsActive;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivateUserAsync(UserSession userSession, UserAccountId userId, CancellationToken cancellationToken)
    {
        return await setUserActivationStateAsync(userSession, userId, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateUserAsync(UserSession userSession, UserAccountId userId, CancellationToken cancellationToken)
    {
        return await setUserActivationStateAsync(userSession, userId, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setUserActivationStateAsync(
        UserSession userSession,
        UserAccountId userAccountId,
        EActivationState activationState,
        CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredUserAccountId = userAccountId.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            ServiceResult administratorValidationResult = validateAdministratorRoleChange(userSession, userAccountOrNull.Role);
            if (administratorValidationResult.IsSuccess == false)
            {
                return administratorValidationResult;
            }

            ServiceResult managementLeadershipValidationResult = validateManagementLeadershipRoleChange(userSession, userAccountOrNull.Role);
            if (managementLeadershipValidationResult.IsSuccess == false)
            {
                return managementLeadershipValidationResult;
            }

            userAccountOrNull.IsActive = activationState == EActivationState.Active;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ResetPasswordAsync(
        UserSession userSession,
        UserAccountId userId,
        PlainPassword newPassword,
        CancellationToken cancellationToken
    )
    {
        return await resetPasswordAsync(userSession, userId, newPassword, cancellationToken);
    }

    private async Task<ServiceResult> resetPasswordAsync(
        UserSession userSession,
        UserAccountId userAccountId,
        PlainPassword newPassword,
        CancellationToken cancellationToken
    )
    {
        if (newPassword.HasMinimumLength(8) == false)
        {
            return ServiceResult.Fail("비밀번호는 8자 이상이어야 합니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredUserAccountId = userAccountId.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            ServiceResult administratorValidationResult = validateAdministratorRoleChange(userSession, userAccountOrNull.Role);
            if (administratorValidationResult.IsSuccess == false)
            {
                return administratorValidationResult;
            }

            ServiceResult managementLeadershipValidationResult = validateManagementLeadershipRoleChange(userSession, userAccountOrNull.Role);
            if (managementLeadershipValidationResult.IsSuccess == false)
            {
                return managementLeadershipValidationResult;
            }

            PasswordHash passwordHash = mPasswordHashService.HashPassword(newPassword);
            userAccountOrNull.PasswordHash = passwordHash.GetRequiredValue();
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }
}
