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

public sealed partial class UserManagementService
{
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
