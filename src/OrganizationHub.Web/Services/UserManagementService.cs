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

public sealed class UserManagementService : IUserManagementService
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
            int? teamIDOrNull = await getAllowedTeamIDAsync(dbContext, formModel, role, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, role, teamIDOrNull, cancellationToken);
            UserAccount userAccount = new UserAccount();
            userAccount.Username = username;
            userAccount.DisplayName = formModel.DisplayName.Trim();
            userAccount.PasswordHash = mPasswordHashService.HashPassword(formModel.Password);
            userAccount.Role = role;
            userAccount.TeamID = teamIDOrNull;
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
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.ID == formModel.ID, cancellationToken);
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
            bool hasDuplicateUsername = await dbContext.UserAccounts.AnyAsync(user => user.ID != formModel.ID && user.Username == username, cancellationToken);
            if (hasDuplicateUsername)
            {
                return ServiceResult.Fail("이미 사용 중인 아이디입니다.");
            }

            userAccountOrNull.Username = username;
            userAccountOrNull.DisplayName = formModel.DisplayName.Trim();
            EUserRole role = getAllowedRole(formModel);
            int? teamIDOrNull = await getAllowedTeamIDAsync(dbContext, formModel, role, cancellationToken);
            OrganizationPosition? positionOrNull = await getAllowedPositionOrNullAsync(dbContext, formModel, role, teamIDOrNull, cancellationToken);
            userAccountOrNull.Role = role;
            userAccountOrNull.TeamID = teamIDOrNull;
            applyUserPosition(userAccountOrNull, positionOrNull);
            userAccountOrNull.IsActive = formModel.IsActive;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<ServiceResult> ActivateUserAsync(UserSession userSession, int userID, CancellationToken cancellationToken)
    {
        return await setUserActivationStateAsync(userSession, userID, EActivationState.Active, cancellationToken);
    }

    public async Task<ServiceResult> DeactivateUserAsync(UserSession userSession, int userID, CancellationToken cancellationToken)
    {
        return await setUserActivationStateAsync(userSession, userID, EActivationState.Inactive, cancellationToken);
    }

    private async Task<ServiceResult> setUserActivationStateAsync(UserSession userSession, int userID, EActivationState activationState, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.ID == userID, cancellationToken);
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

    public async Task<ServiceResult> ResetPasswordAsync(UserSession userSession, int userID, string newPassword, CancellationToken cancellationToken)
    {
        if (newPassword.Length < 8)
        {
            return ServiceResult.Fail("비밀번호는 8자 이상이어야 합니다.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            UserAccount? userAccountOrNull = await dbContext.UserAccounts.FirstOrDefaultAsync(user => user.ID == userID, cancellationToken);
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

            userAccountOrNull.PasswordHash = mPasswordHashService.HashPassword(newPassword);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

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

    private async Task<int?> getAllowedTeamIDAsync(OrganizationHubDbContext dbContext, UserAccountFormModel formModel, EUserRole role, CancellationToken cancellationToken)
    {
        if (role == EUserRole.Admin)
        {
            return await getSystemAdministrationTeamIDAsync(dbContext, cancellationToken);
        }

        return formModel.TeamID;
    }

    private async Task<OrganizationPosition?> getAllowedPositionOrNullAsync(OrganizationHubDbContext dbContext, UserAccountFormModel formModel, EUserRole role, int? teamIDOrNull, CancellationToken cancellationToken)
    {
        if (teamIDOrNull.HasValue == false)
        {
            return null;
        }

        if (role == EUserRole.Admin)
        {
            return await getSystemAdministratorPositionOrNullAsync(dbContext, teamIDOrNull.Value, cancellationToken);
        }

        if (formModel.PositionID.HasValue)
        {
            OrganizationPosition? selectedPositionOrNull = await dbContext.Positions.FirstOrDefaultAsync(position =>
                position.ID == formModel.PositionID.Value &&
                position.TeamID == teamIDOrNull.Value &&
                position.IsActive &&
                position.IsSystemAdministrator == false,
                cancellationToken);
            if (selectedPositionOrNull != null)
            {
                return selectedPositionOrNull;
            }
        }

        OrganizationPosition? defaultPositionOrNull = await dbContext.Positions
            .Where(position => position.TeamID == teamIDOrNull.Value && position.IsActive && position.IsSystemAdministrator == false)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return defaultPositionOrNull;
    }

    private async Task<OrganizationPosition?> getSystemAdministratorPositionOrNullAsync(OrganizationHubDbContext dbContext, int teamID, CancellationToken cancellationToken)
    {
        OrganizationPosition? positionOrNull = await dbContext.Positions
            .Include(position => position.PermissionGrants)
            .FirstOrDefaultAsync(position => position.TeamID == teamID && position.IsSystemAdministrator, cancellationToken);
        if (positionOrNull == null)
        {
            positionOrNull = new OrganizationPosition();
            positionOrNull.TeamID = teamID;
            positionOrNull.Name = "시스템 관리자";
            positionOrNull.Description = "조직 전체 설정과 운영 데이터를 관리하는 기본 관리자 직책입니다.";
            positionOrNull.SortOrder = 0;
            positionOrNull.IsActive = true;
            positionOrNull.IsSystemAdministrator = true;
            dbContext.Positions.Add(positionOrNull);
        }

        positionOrNull.IsActive = true;
        addMissingSystemAdministratorPermissionGrants(dbContext, positionOrNull);
        return positionOrNull;
    }

    private void addMissingSystemAdministratorPermissionGrants(OrganizationHubDbContext dbContext, OrganizationPosition position)
    {
        List<string> existingPermissionKeys = position.PermissionGrants
            .Where(permissionGrant => permissionGrant.ScopeType == EPermissionScopeType.Global && permissionGrant.TargetTeamID.HasValue == false)
            .Select(permissionGrant => permissionGrant.PermissionKey)
            .ToList();
        IReadOnlyList<PermissionDefinition> permissionDefinitions = PermissionCatalog.GetDefinitions();
        foreach (PermissionDefinition permissionDefinition in permissionDefinitions)
        {
            if (existingPermissionKeys.Contains(permissionDefinition.Key))
            {
                continue;
            }

            PositionPermissionGrant permissionGrant = new PositionPermissionGrant();
            permissionGrant.Position = position;
            permissionGrant.PermissionKey = permissionDefinition.Key;
            permissionGrant.ScopeType = EPermissionScopeType.Global;
            permissionGrant.IsAllowed = true;
            dbContext.PositionPermissionGrants.Add(permissionGrant);
        }
    }

    private void applyUserPosition(UserAccount userAccount, OrganizationPosition? positionOrNull)
    {
        userAccount.Position = positionOrNull;
        if (positionOrNull == null)
        {
            userAccount.PositionID = null;
        }
    }

    private static bool isSystemAdministrationTeam(Team team)
    {
        return string.Equals(team.TeamKey, OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> getSystemAdministrationTeamIDAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        Team? systemAdministrationTeamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team =>
            team.TeamKey == OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY,
            cancellationToken);
        if (systemAdministrationTeamOrNull != null)
        {
            systemAdministrationTeamOrNull.DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME;
            systemAdministrationTeamOrNull.SortOrder = 0;
            systemAdministrationTeamOrNull.IsActive = true;
            return systemAdministrationTeamOrNull.ID;
        }

        Team systemAdministrationTeam = new Team();
        systemAdministrationTeam.TeamKey = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY;
        systemAdministrationTeam.DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME;
        systemAdministrationTeam.SortOrder = 0;
        systemAdministrationTeam.IsActive = true;
        dbContext.Teams.Add(systemAdministrationTeam);
        await dbContext.SaveChangesAsync(cancellationToken);
        return systemAdministrationTeam.ID;
    }
}
