using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDataSeeder
{
    private const int SYSTEM_ADMINISTRATION_SORT_ORDER = 0;
    private const int MANAGEMENT_SORT_ORDER = 10;

    private UserAccount createSeedUser(Team systemAdministrationTeam, OrganizationPosition administratorPosition)
    {
        string seedUsername = getConfiguredSeedUsername();
        string seedPassword = getConfiguredSeedPassword();
        UserAccount userAccount = new UserAccount();
        userAccount.Username = seedUsername;
        userAccount.DisplayName = getConfiguredSeedDisplayName();
        userAccount.PasswordHash = mPasswordHashService.HashPassword(PlainPassword.CreateFromInput(seedPassword)).GetRequiredValue();
        userAccount.StoredCompatibilityRole = EStoredCompatibilityRole.Admin;
        userAccount.Team = systemAdministrationTeam;
        userAccount.Position = administratorPosition;
        userAccount.IsActive = true;
        userAccount.CreatedAt = DateTime.UtcNow;
        return userAccount;
    }

    private async Task<Team> ensureSystemAdministrationTeamAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        Team? systemAdministrationTeamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team =>
            team.TeamKey == OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY,
            cancellationToken);
        if (systemAdministrationTeamOrNull == null)
        {
            Team systemAdministrationTeam = createTeam(new TeamSeedDefinition
            {
                TeamKey = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY,
                DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME,
                SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER
            });
            dbContext.Teams.Add(systemAdministrationTeam);
            return systemAdministrationTeam;
        }

        systemAdministrationTeamOrNull.DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME;
        systemAdministrationTeamOrNull.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
        systemAdministrationTeamOrNull.IsActive = true;
        return systemAdministrationTeamOrNull;
    }

    private async Task<Team> ensureManagementTeamAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        Team? managementTeamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team =>
            team.TeamKey == OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY,
            cancellationToken);
        if (managementTeamOrNull == null)
        {
            Team managementTeam = createTeam(new TeamSeedDefinition
            {
                TeamKey = OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY,
                DisplayName = OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME,
                SortOrder = MANAGEMENT_SORT_ORDER
            });
            dbContext.Teams.Add(managementTeam);
            return managementTeam;
        }

        managementTeamOrNull.DisplayName = OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME;
        managementTeamOrNull.SortOrder = MANAGEMENT_SORT_ORDER;
        managementTeamOrNull.IsActive = true;
        return managementTeamOrNull;
    }

    private async Task ensureDefaultManagementPositionAsync(
        OrganizationHubDbContext dbContext,
        Team managementTeam,
        CancellationToken cancellationToken
    )
    {
        bool hasManagementPosition = await dbContext.Positions.AnyAsync(position => position.TeamId == managementTeam.Id && position.IsSystemAdministrator == false, cancellationToken);
        if (hasManagementPosition)
        {
            return;
        }

        OrganizationPosition position = new OrganizationPosition();
        position.Team = managementTeam;
        position.Name = "관리팀 구성원";
        position.Description = "시스템 관리자가 권한을 부여해 사용하는 관리팀 기본 직책입니다.";
        position.SortOrder = MANAGEMENT_SORT_ORDER;
        position.IsActive = true;
        position.IsSystemAdministrator = false;
        dbContext.Positions.Add(position);
    }

    private async Task<OrganizationPosition> ensureSystemAdministratorPositionAsync(
        OrganizationHubDbContext dbContext,
        Team team,
        CancellationToken cancellationToken
    )
    {
        OrganizationPosition? positionOrNull = await dbContext.Positions.Include(position => position.PermissionGrants).FirstOrDefaultAsync(position => position.TeamId == team.Id && position.IsSystemAdministrator, cancellationToken);
        if (positionOrNull == null)
        {
            OrganizationPosition position = new OrganizationPosition();
            position.Team = team;
            position.Name = "시스템 관리자";
            position.Description = "Organization Hub 전체 설정과 권한을 관리하는 기본 직책입니다.";
            position.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
            position.IsActive = true;
            position.IsSystemAdministrator = true;
            dbContext.Positions.Add(position);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ensureSystemAdministratorPermissionGrantsAsync(dbContext, position, cancellationToken);
            return position;
        }

        positionOrNull.Name = "시스템 관리자";
        positionOrNull.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
        positionOrNull.IsActive = true;
        positionOrNull.IsSystemAdministrator = true;
        await ensureSystemAdministratorPermissionGrantsAsync(dbContext, positionOrNull, cancellationToken);
        return positionOrNull;
    }

    private async Task ensureSystemAdministratorPermissionGrantsAsync(
        OrganizationHubDbContext dbContext,
        OrganizationPosition position,
        CancellationToken cancellationToken
    )
    {
        List<string> existingPermissionKeys = await dbContext.PositionPermissionGrants.Where(permissionGrant => permissionGrant.PositionId == position.Id).Select(permissionGrant => permissionGrant.PermissionKey).ToListAsync(cancellationToken);
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

    private async Task attachOrganizationAdministratorUsersToSystemAdministrationTeamAsync(
        OrganizationHubDbContext dbContext,
        Team systemAdministrationTeam,
        CancellationToken cancellationToken
    )
    {
        List<UserAccount> organizationAdministratorUsers = await dbContext.UserAccounts.Where(user =>
            user.TeamId.HasValue == false &&
            user.StoredCompatibilityRole == EStoredCompatibilityRole.Admin).ToListAsync(cancellationToken);
        foreach (UserAccount userAccount in organizationAdministratorUsers)
        {
            userAccount.Team = systemAdministrationTeam;
        }
    }

    private async Task attachUnassignedOrganizationMembersToManagementTeamAsync(
        OrganizationHubDbContext dbContext,
        Team managementTeam,
        CancellationToken cancellationToken
    )
    {
        List<UserAccount> organizationMembers = await dbContext.UserAccounts.Where(user =>
            user.TeamId.HasValue == false &&
            user.StoredCompatibilityRole != EStoredCompatibilityRole.Admin).ToListAsync(cancellationToken);
        foreach (UserAccount userAccount in organizationMembers)
        {
            userAccount.Team = managementTeam;
        }
    }

    private async Task attachAdministratorsToSystemAdministratorPositionAsync(
        OrganizationHubDbContext dbContext,
        OrganizationPosition administratorPosition,
        CancellationToken cancellationToken
    )
    {
        List<UserAccount> administratorUsers = await dbContext.UserAccounts.Where(user => user.StoredCompatibilityRole == EStoredCompatibilityRole.Admin && user.PositionId.HasValue == false).ToListAsync(cancellationToken);
        foreach (UserAccount userAccount in administratorUsers)
        {
            userAccount.Position = administratorPosition;
        }
    }

    private async Task upgradeDefaultAdministratorDisplayNamesAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        string configuredDisplayName = getConfiguredSeedDisplayName();
        if (configuredDisplayName == PREVIOUS_SEED_ADMINISTRATOR_DISPLAY_NAME)
        {
            return;
        }

        List<UserAccount> administratorUsers = await dbContext.UserAccounts.Where(user =>
            user.StoredCompatibilityRole == EStoredCompatibilityRole.Admin &&
            user.DisplayName == PREVIOUS_SEED_ADMINISTRATOR_DISPLAY_NAME).ToListAsync(cancellationToken);
        foreach (UserAccount userAccount in administratorUsers)
        {
            userAccount.DisplayName = configuredDisplayName;
        }
    }

    private Team createTeam(TeamSeedDefinition definition)
    {
        Team team = new Team();
        team.TeamKey = definition.TeamKey;
        team.DisplayName = definition.DisplayName;
        team.SortOrder = definition.SortOrder;
        team.IsActive = true;
        return team;
    }
}
