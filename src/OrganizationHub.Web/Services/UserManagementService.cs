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
    private const EStoredCompatibilityRole STANDARD_USER_STORED_COMPATIBILITY_ROLE = EStoredCompatibilityRole.TeamUser;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPasswordHashService mPasswordHashService;

    public UserManagementService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPasswordHashService passwordHashService)
    {
        mDbContextFactory = dbContextFactory;
        mPasswordHashService = passwordHashService;
    }

    public async Task<bool> CanManageSystemAdministratorsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        return await isSystemAdministratorAsync(userSession, cancellationToken);
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            IQueryable<UserAccount> query = dbContext.UserAccounts.Include(user => user.Team).Include(user => user.Position);
            bool canManageSystemAdministrators = await isSystemAdministratorAsync(userSession, cancellationToken);
            if (canManageSystemAdministrators == false)
            {
                query = query.Where(user => user.Position == null || user.Position.IsSystemAdministrator == false);
            }

            List<UserAccount> users = await query.OrderBy(user => user.Username).ToListAsync(cancellationToken);
            return users;
        }
    }

    public async Task<ServiceResult> CreateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult requestedAdministratorValidationResult = await validateRequestedSystemAdministratorAccountChangeAsync(userSession, formModel, cancellationToken);
        if (requestedAdministratorValidationResult.IsSuccess == false)
        {
            return requestedAdministratorValidationResult;
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

            EUserAccountKind accountKind = formModel.AccountKind;
            EStoredCompatibilityRole storedCompatibilityRole = createStoredCompatibilityRole(accountKind);
            OrganizationTeamId? teamIdOrNull = await getAllowedTeamIdOrNullAsync(dbContext, formModel, accountKind, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, accountKind, teamIdOrNull, cancellationToken);
            ServiceResult resolvedAdministratorValidationResult = await validateSystemAdministratorAccountChangeAsync(userSession, positionOrNull, cancellationToken);
            if (resolvedAdministratorValidationResult.IsSuccess == false)
            {
                return resolvedAdministratorValidationResult;
            }

            UserAccount userAccount = new UserAccount();
            userAccount.Username = username;
            userAccount.DisplayName = formModel.DisplayName.Trim();
            PasswordHash passwordHash = mPasswordHashService.HashPassword(PlainPassword.CreateFromInput(formModel.Password));
            userAccount.PasswordHash = passwordHash.GetRequiredValue();
            userAccount.StoredCompatibilityRole = storedCompatibilityRole;
            userAccount.TeamId = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
            applyUserPosition(userAccount, positionOrNull);
            userAccount.IsActive = formModel.IsActive;
            userAccount.CreatedAt = DateTime.UtcNow;
            dbContext.UserAccounts.Add(userAccount);
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }

    public async Task<ServiceResult> UpdateUserAsync(UserSession userSession, UserAccountFormModel formModel, CancellationToken cancellationToken)
    {
        ServiceResult requestedAdministratorValidationResult = await validateRequestedSystemAdministratorAccountChangeAsync(userSession, formModel, cancellationToken);
        if (requestedAdministratorValidationResult.IsSuccess == false)
        {
            return requestedAdministratorValidationResult;
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
            UserAccount? userAccountOrNull = await dbContext.UserAccounts
                .Include(user => user.Position)
                .FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            if (userAccountOrNull.Revision != formModel.Revision)
            {
                return AdministrativeSaveUtility.CreateConflictResult();
            }

            ServiceResult existingAdministratorValidationResult = await validateSystemAdministratorAccountChangeAsync(userSession, userAccountOrNull, cancellationToken);
            if (existingAdministratorValidationResult.IsSuccess == false)
            {
                return existingAdministratorValidationResult;
            }

            string username = formModel.Username.Trim();
            bool hasDuplicateUsername = await dbContext.UserAccounts.AnyAsync(user => user.Id != requiredUserAccountId && user.Username == username, cancellationToken);
            if (hasDuplicateUsername)
            {
                return ServiceResult.Fail("이미 사용 중인 아이디입니다.");
            }

            userAccountOrNull.Username = username;
            userAccountOrNull.DisplayName = formModel.DisplayName.Trim();
            EUserAccountKind accountKind = formModel.AccountKind;
            EStoredCompatibilityRole storedCompatibilityRole = createStoredCompatibilityRole(accountKind);
            OrganizationTeamId? teamIdOrNull = await getAllowedTeamIdOrNullAsync(dbContext, formModel, accountKind, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, accountKind, teamIdOrNull, cancellationToken);
            ServiceResult resolvedAdministratorValidationResult = await validateSystemAdministratorAccountChangeAsync(userSession, positionOrNull, cancellationToken);
            if (resolvedAdministratorValidationResult.IsSuccess == false)
            {
                return resolvedAdministratorValidationResult;
            }

            userAccountOrNull.StoredCompatibilityRole = storedCompatibilityRole;
            userAccountOrNull.TeamId = OrganizationTeamId.GetValueOrNull(teamIdOrNull);
            applyUserPosition(userAccountOrNull, positionOrNull);
            userAccountOrNull.IsActive = formModel.IsActive;
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
            UserAccount? userAccountOrNull = await dbContext.UserAccounts
                .Include(user => user.Position)
                .FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            ServiceResult administratorValidationResult = await validateSystemAdministratorAccountChangeAsync(userSession, userAccountOrNull, cancellationToken);
            if (administratorValidationResult.IsSuccess == false)
            {
                return administratorValidationResult;
            }

            userAccountOrNull.IsActive = activationState == EActivationState.Active;
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
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
        if (newPassword.HasRequiredMinimumLength() == false)
        {
            return ServiceResult.Fail("비밀번호는 8자 이상이어야 합니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            int requiredUserAccountId = userAccountId.GetRequiredValue();
            UserAccount? userAccountOrNull = await dbContext.UserAccounts
                .Include(user => user.Position)
                .FirstOrDefaultAsync(user => user.Id == requiredUserAccountId, cancellationToken);
            if (userAccountOrNull == null)
            {
                return ServiceResult.Fail("사용자를 찾을 수 없습니다.");
            }

            ServiceResult administratorValidationResult = await validateSystemAdministratorAccountChangeAsync(userSession, userAccountOrNull, cancellationToken);
            if (administratorValidationResult.IsSuccess == false)
            {
                return administratorValidationResult;
            }

            PasswordHash passwordHash = mPasswordHashService.HashPassword(newPassword);
            userAccountOrNull.PasswordHash = passwordHash.GetRequiredValue();
            return await AdministrativeSaveUtility.SaveAsync(dbContext, cancellationToken);
        }
    }
}
