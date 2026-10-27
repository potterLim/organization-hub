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

internal sealed partial class NavigationMenuService
{
    public async Task<IReadOnlyList<NavigationMenuItem>> GetVisibleMenuItemsAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.Section)
                .Include(menuItem => menuItem.TeamPermissions)
                .ThenInclude(menuPermission => menuPermission.Team)
                .OrderBy(menuItem => menuItem.Section == null ? int.MaxValue : menuItem.Section.SortOrder)
                .ThenBy(menuItem => menuItem.SortOrder)
                .ThenBy(menuItem => menuItem.DisplayName)
                .ToListAsync(cancellationToken);
            List<NavigationMenuItem> visibleMenuItems = new List<NavigationMenuItem>();
            NavigationMenuVisibilityEvaluationContext evaluationContext = await createVisibilityEvaluationContextAsync(userSession, cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (await canUseMenuItemAsync(menuItem, evaluationContext, cancellationToken))
                {
                    visibleMenuItems.Add(menuItem);
                }
            }

            List<NavigationMenuItem> orderedVisibleMenuItems = visibleMenuItems
                .OrderBy(menuItem => menuItem.Section == null ? int.MaxValue : menuItem.Section.SortOrder)
                .ThenBy(menuItem => menuItem.SortOrder)
                .ThenBy(menuItem => menuItem.DisplayName)
                .ToList();
            return orderedVisibleMenuItems;
        }
    }

    private async Task<bool> canUseMenuItemAsync(
        NavigationMenuItem menuItem,
        NavigationMenuVisibilityEvaluationContext evaluationContext,
        CancellationToken cancellationToken
    )
    {
        if (menuItem.IsActive == false && menuItem.IsLocked == false)
        {
            return false;
        }

        if (evaluationContext.CanBypassMenuVisibility)
        {
            return true;
        }

        UserSession userSession = evaluationContext.UserSession;
        bool canUseMenuPermission = await canUseMenuPermissionAsync(menuItem, userSession, cancellationToken);
        if (canUseMenuPermission == false)
        {
            return false;
        }

        if (isManagementFilesMenu(menuItem) && isManagementFilesUser(userSession) == false)
        {
            return false;
        }

        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel) == false)
        {
            return true;
        }

        if (menuItem.IsVisibleToAllTeams)
        {
            return true;
        }

        if (userSession.HasTeam == false)
        {
            return false;
        }

        int teamId = userSession.GetRequiredOrganizationTeamId().GetRequiredValue();
        foreach (NavigationMenuTeamPermission permission in menuItem.TeamPermissions)
        {
            if (permission.TeamId == teamId && permission.Team != null && permission.Team.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<NavigationMenuVisibilityEvaluationContext> createVisibilityEvaluationContextAsync(
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        bool canBypassMenuVisibility = await isSystemAdministratorAsync(userSession, cancellationToken);
        return new NavigationMenuVisibilityEvaluationContext
        {
            UserSession = userSession,
            CanBypassMenuVisibility = canBypassMenuVisibility
        };
    }

    private async Task<bool> canUseMenuPermissionAsync(
        NavigationMenuItem menuItem,
        UserSession userSession,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<PermissionKey> permissionKeys = NavigationMenuPermissionKeys.ParseStorageValue(menuItem.RequiredPermissionKeys);
        if (permissionKeys.Count == 0)
        {
            return true;
        }

        OrganizationTeamId? targetTeamIdOrNull = getConfiguredPermissionTargetTeamIdOrNull(menuItem, userSession);
        foreach (PermissionKey permissionKey in permissionKeys)
        {
            string permissionValue = permissionKey.GetRequiredValue();
            UserAccountId? ownerUserIdOrNull = permissionValue == PermissionKeys.CALENDAR_READ || permissionValue == PermissionKeys.TODOS_READ ? userSession.UserAccountId : null;
            PermissionDecision decision = await mOrganizationPermissionService.DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, ownerUserIdOrNull, cancellationToken);
            bool hasPermission = decision.IsAllowed;
            if (hasPermission)
            {
                return true;
            }
            if (await canReadDelegatedResourceAsync(menuItem, userSession, permissionKey, ownerUserIdOrNull, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> canReadDelegatedResourceAsync(NavigationMenuItem menuItem, UserSession userSession, PermissionKey permissionKey, UserAccountId? ownerUserIdOrNull, CancellationToken cancellationToken)
    {
        string path = menuItem.Href;
        string permission = permissionKey.GetRequiredValue();
        bool isResourceBrowser = (path == "/calendar" && permission == PermissionKeys.CALENDAR_READ)
            || (path == "/todos" && permission == PermissionKeys.TODOS_READ)
            || (path == "/materials" && permission == PermissionKeys.FILES_READ);
        if (isResourceBrowser == false)
        {
            return false;
        }
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<int> activeTeamIds = await dbContext.Teams.Where(team => team.IsActive).Select(team => team.Id).ToListAsync(cancellationToken);
            foreach (int teamId in activeTeamIds)
            {
                PermissionDecision decision = await mOrganizationPermissionService.DecidePermissionAsync(userSession, permissionKey, OrganizationTeamId.CreateExisting(teamId), ownerUserIdOrNull, cancellationToken);
                if (decision.IsAllowed)
                {
                    return true;
                }
            }
        }
        return false;
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
        NavigationMenuKey menuKey = NavigationMenuKey.CreateOptional(menuItem.MenuKey);
        switch (menuKey.GetOptionalValue())
        {
            case NavigationMenuKey.CALENDAR:
            case NavigationMenuKey.TODOS:
            case NavigationMenuKey.APPROVAL_INBOX:
            case NavigationMenuKey.APPROVAL_HISTORY:
            case NavigationMenuKey.MANAGEMENT_FILES:
            case NavigationMenuKey.WORK_GUIDELINES:
                return true;
            default:
                return false;
        }
    }

    private bool isManagementFilesMenu(NavigationMenuItem menuItem)
    {
        NavigationMenuKey menuKey = NavigationMenuKey.CreateOptional(menuItem.MenuKey);
        return menuKey.Is(NavigationMenuKey.ManagementFiles);
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
                        userAccount.IsActive,
                    cancellationToken);
            return userAccountOrNull?.Position != null &&
                userAccountOrNull.Position.IsActive &&
                userAccountOrNull.Position.IsSystemAdministrator;
        }
    }

    private bool isManagementFilesUser(UserSession userSession)
    {
        return userSession.TeamKey.IsManagementTeamKey;
    }
}
