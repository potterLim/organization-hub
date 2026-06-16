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
            EPermissionConfigurationMode configurationMode = await getNavigationPermissionConfigurationModeAsync(userSession, cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (await canUseMenuItemAsync(menuItem, userSession, configurationMode, cancellationToken))
                {
                    visibleMenuItems.Add(menuItem);
                }
            }

            List<NavigationMenuItem> orderedVisibleMenuItems = visibleMenuItems.OrderBy(menuItem => getVisibleMenuGroupOrder(menuItem)).ThenBy(menuItem => menuItem.SortOrder).ThenBy(menuItem => menuItem.DisplayName).ToList();
            return orderedVisibleMenuItems;
        }
    }

    private async Task<bool> canUseMenuItemAsync(
        NavigationMenuItem menuItem,
        UserSession userSession,
        EPermissionConfigurationMode configurationMode,
        CancellationToken cancellationToken
    )
    {
        if (menuItem.IsActive == false && menuItem.IsLocked == false)
        {
            return false;
        }

        bool canUseMenuPermission = await canUseMenuPermissionAsync(menuItem, userSession, configurationMode, cancellationToken);
        if (canUseMenuPermission == false)
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

        if (userSession.HasTeam == false)
        {
            return false;
        }

        int teamId = userSession.GetRequiredTeamIdValue();
        foreach (NavigationMenuTeamPermission permission in menuItem.TeamPermissions)
        {
            if (permission.TeamId == teamId && permission.Team != null && permission.Team.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> canUseMenuPermissionAsync(
        NavigationMenuItem menuItem,
        UserSession userSession,
        EPermissionConfigurationMode configurationMode,
        CancellationToken cancellationToken
    )
    {
        if (configurationMode == EPermissionConfigurationMode.ConfiguredPermissions)
        {
            IReadOnlyList<string> permissionKeys = NavigationMenuPermissionCatalog.GetRequiredPermissionKeys(menuItem.MenuKey);
            if (permissionKeys.Count == 0)
            {
                return true;
            }

            OrganizationTeamId? targetTeamIdOrNull = getConfiguredPermissionTargetTeamIdOrNull(menuItem, userSession);
            foreach (string permissionKey in permissionKeys)
            {
                bool hasPermission = await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(permissionKey), targetTeamIdOrNull, cancellationToken);
                if (hasPermission)
                {
                    return true;
                }
            }

            return false;
        }

        return MenuAccessLevelUtility.IsRoleAllowed(menuItem.AccessLevel, userSession.Role);
    }

    private async Task<EPermissionConfigurationMode> getNavigationPermissionConfigurationModeAsync(
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        if (usesConfiguredPermissions)
        {
            return EPermissionConfigurationMode.ConfiguredPermissions;
        }

        return EPermissionConfigurationMode.FallbackPolicy;
    }

    private OrganizationTeamId? getConfiguredPermissionTargetTeamIdOrNull(NavigationMenuItem menuItem, UserSession userSession)
    {
        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel))
        {
            return createUserTeamIdOrNull(userSession);
        }

        if (shouldUseUserTeamContext(menuItem))
        {
            return createUserTeamIdOrNull(userSession);
        }

        return null;
    }

    private OrganizationTeamId? createUserTeamIdOrNull(UserSession userSession)
    {
        if (userSession.HasTeam == false)
        {
            return null;
        }

        return userSession.GetRequiredOrganizationTeamId();
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

        return userSession.TeamKey.IsManagementTeamKey;
    }
}
