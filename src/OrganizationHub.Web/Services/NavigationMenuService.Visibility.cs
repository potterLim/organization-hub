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

public sealed partial class NavigationMenuService
{
    public async Task<IReadOnlyList<NavigationMenuItem>> GetVisibleMenuItemsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).ThenInclude(menuPermission => menuPermission.Team).OrderBy(menuItem => menuItem.SortOrder).ThenBy(menuItem => menuItem.DisplayName).ToListAsync(cancellationToken);
            List<NavigationMenuItem> visibleMenuItems = new List<NavigationMenuItem>();
            bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (await isVisibleForUserAsync(menuItem, userSession, usesConfiguredPermissions, cancellationToken))
                {
                    visibleMenuItems.Add(menuItem);
                }
            }

            List<NavigationMenuItem> orderedVisibleMenuItems = visibleMenuItems.OrderBy(menuItem => getVisibleMenuGroupOrder(menuItem)).ThenBy(menuItem => menuItem.SortOrder).ThenBy(menuItem => menuItem.DisplayName).ToList();
            return orderedVisibleMenuItems;
        }
    }

    private async Task<bool> isVisibleForUserAsync(NavigationMenuItem menuItem, UserSession userSession, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        if (menuItem.IsActive == false && menuItem.IsLocked == false)
        {
            return false;
        }

        bool isAccessAllowed = await isAccessLevelAllowedAsync(menuItem, userSession, usesConfiguredPermissions, cancellationToken);
        if (isAccessAllowed == false)
        {
            return false;
        }

        if (isManagementFilesMenu(menuItem) && isManagementFilesUser(userSession) == false)
        {
            return false;
        }

        if (userSession.IsManagementRole())
        {
            return true;
        }

        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel) == false)
        {
            return true;
        }

        if (menuItem.IsVisibleToAllTeams)
        {
            return true;
        }

        if (userSession.IsTeamRole() == false)
        {
            return false;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        int teamID = userSession.TeamID.Value;
        foreach (NavigationMenuTeamPermission permission in menuItem.TeamPermissions)
        {
            if (permission.TeamID == teamID && permission.Team != null && permission.Team.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> isAccessLevelAllowedAsync(NavigationMenuItem menuItem, UserSession userSession, bool usesConfiguredPermissions, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions)
        {
            IReadOnlyList<string> permissionKeys = getMenuPermissionKeys(menuItem);
            if (permissionKeys.Count == 0)
            {
                return true;
            }

            int? targetTeamIDOrNull = getConfiguredPermissionTargetTeamIDOrNull(menuItem, userSession);
            foreach (string permissionKey in permissionKeys)
            {
                bool hasPermission = await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(permissionKey), targetTeamIDOrNull, cancellationToken);
                if (hasPermission)
                {
                    return true;
                }
            }

            return false;
        }

        return MenuAccessLevelUtility.IsRoleAllowed(menuItem.AccessLevel, userSession.Role);
    }

    private int? getConfiguredPermissionTargetTeamIDOrNull(NavigationMenuItem menuItem, UserSession userSession)
    {
        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel))
        {
            return userSession.TeamID;
        }

        if (shouldUseUserTeamContext(menuItem))
        {
            return userSession.TeamID;
        }

        return null;
    }

    private bool shouldUseUserTeamContext(NavigationMenuItem menuItem)
    {
        switch (menuItem.MenuKey)
        {
            case "Calendar":
            case "Todos":
            case "ApprovalInbox":
            case "ApprovalHistory":
            case "ManagementFiles":
            case "WorkGuidelines":
                return true;
            default:
                return false;
        }
    }

    private bool isManagementFilesMenu(NavigationMenuItem menuItem)
    {
        return string.Equals(menuItem.MenuKey, "ManagementFiles", StringComparison.OrdinalIgnoreCase);
    }

    private bool isManagementFilesUser(UserSession userSession)
    {
        if (userSession.Role == EUserRole.Admin || userSession.IsManagementRole())
        {
            return true;
        }

        return string.Equals(userSession.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }
}
