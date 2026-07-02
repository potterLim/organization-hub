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

public sealed partial class UserManagementService
{
    private const int SYSTEM_ADMINISTRATION_SORT_ORDER = 0;

    private async Task<OrganizationTeamId?> getAllowedTeamIdOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserAccountFormModel formModel,
        EUserAccountKind accountKind,
        CancellationToken cancellationToken)
    {
        if (accountKind == EUserAccountKind.SystemAdministrator)
        {
            return await getSystemAdministrationTeamIdAsync(dbContext, cancellationToken);
        }

        OrganizationTeamId teamId = formModel.TeamIdOrNull.GetValueOrDefault();
        if (teamId.HasValue == false)
        {
            return null;
        }

        return teamId;
    }

    private async Task<OrganizationPosition?> getAllowedPositionOrNullAsync(
        OrganizationHubDbContext dbContext,
        UserAccountFormModel formModel,
        EUserAccountKind accountKind,
        OrganizationTeamId? teamIdOrNull,
        CancellationToken cancellationToken)
    {
        OrganizationTeamId teamId = teamIdOrNull.GetValueOrDefault();
        if (teamId.HasValue == false)
        {
            return null;
        }

        int requiredTeamId = teamId.GetRequiredValue();
        if (accountKind == EUserAccountKind.SystemAdministrator)
        {
            return await getSystemAdministratorPositionOrNullAsync(dbContext, teamId, cancellationToken);
        }

        OrganizationPositionId? positionIdOrNull = formModel.PositionIdOrNull;
        int? positionIdValueOrNull = OrganizationPositionId.GetValueOrNull(positionIdOrNull);
        if (positionIdValueOrNull.HasValue)
        {
            OrganizationPosition? selectedPositionOrNull = await dbContext.Positions.FirstOrDefaultAsync(position =>
                position.Id == positionIdValueOrNull.GetValueOrDefault() &&
                position.TeamId == requiredTeamId &&
                position.IsActive &&
                position.IsSystemAdministrator == false,
                cancellationToken);
            if (selectedPositionOrNull != null)
            {
                return selectedPositionOrNull;
            }
        }

        OrganizationPosition? defaultPositionOrNull = await dbContext.Positions
            .Where(position => position.TeamId == requiredTeamId && position.IsActive && position.IsSystemAdministrator == false)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return defaultPositionOrNull;
    }

    private async Task<OrganizationPosition?> getSystemAdministratorPositionOrNullAsync(
        OrganizationHubDbContext dbContext,
        OrganizationTeamId teamId,
        CancellationToken cancellationToken)
    {
        int requiredTeamId = teamId.GetRequiredValue();
        OrganizationPosition? positionOrNull = await dbContext.Positions
            .Include(position => position.PermissionGrants)
            .FirstOrDefaultAsync(position => position.TeamId == requiredTeamId && position.IsSystemAdministrator, cancellationToken);
        if (positionOrNull == null)
        {
            positionOrNull = new OrganizationPosition();
            positionOrNull.TeamId = requiredTeamId;
            positionOrNull.Name = "시스템 관리자";
            positionOrNull.Description = "조직 전체 설정과 운영 데이터를 관리하는 기본 관리자 직책입니다.";
            positionOrNull.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
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
            .Where(permissionGrant => permissionGrant.ScopeType == EPermissionScopeType.Global && permissionGrant.TargetTeamId.HasValue == false)
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
            userAccount.PositionId = null;
        }
    }

    private static bool isSystemAdministrationTeam(Team team)
    {
        return new OrganizationTeamKey(team.TeamKey).IsSystemAdministrationTeamKey;
    }

    private async Task<OrganizationTeamId> getSystemAdministrationTeamIdAsync(
        OrganizationHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Team? systemAdministrationTeamOrNull = await dbContext.Teams.FirstOrDefaultAsync(team =>
            team.TeamKey == OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY,
            cancellationToken);
        if (systemAdministrationTeamOrNull != null)
        {
            systemAdministrationTeamOrNull.DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME;
            systemAdministrationTeamOrNull.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
            systemAdministrationTeamOrNull.IsActive = true;
            return OrganizationTeamId.CreateExisting(systemAdministrationTeamOrNull.Id);
        }

        Team systemAdministrationTeam = new Team();
        systemAdministrationTeam.TeamKey = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY;
        systemAdministrationTeam.DisplayName = OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_DISPLAY_NAME;
        systemAdministrationTeam.SortOrder = SYSTEM_ADMINISTRATION_SORT_ORDER;
        systemAdministrationTeam.IsActive = true;
        dbContext.Teams.Add(systemAdministrationTeam);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OrganizationTeamId.CreateExisting(systemAdministrationTeam.Id);
    }
}
