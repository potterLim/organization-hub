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

public sealed class NavigationMenuService : INavigationMenuService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public NavigationMenuService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
        : this(dbContextFactory, new OrganizationPermissionService(dbContextFactory))
    {
    }

    public NavigationMenuService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mOrganizationPermissionService = organizationPermissionService;
    }

    public async Task<IReadOnlyList<NavigationMenuItem>> GetManageableMenuItemsAsync(CancellationToken cancellationToken)
    {
        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).ThenInclude(menuPermission => menuPermission.Team).ToListAsync(cancellationToken);
            List<NavigationMenuItem> manageableMenuItems = menuItems
                .Where(menuItem => NavigationMenuRetirementPolicy.IsRetired(menuItem.MenuKey, menuItem.Href) == false)
                .OrderBy(menuItem => menuItem.SortOrder)
                .ThenBy(menuItem => menuItem.DisplayName)
                .ToList();
            return manageableMenuItems;
        }
    }

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

    public async Task<ServiceResult> UpdateMenuItemAsync(NavigationMenuItemFormModel formModel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(formModel.DisplayName))
        {
            return ServiceResult.Fail("메뉴 표시 이름을 입력해 주세요.");
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            NavigationMenuItem? menuItemOrNull = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).FirstOrDefaultAsync(menuItem => menuItem.ID == formModel.ID, cancellationToken);
            if (menuItemOrNull == null)
            {
                return ServiceResult.Fail("메뉴 항목을 찾을 수 없습니다.");
            }

            menuItemOrNull.DisplayName = formModel.DisplayName.Trim();
            menuItemOrNull.SortOrder = formModel.SortOrder;
            menuItemOrNull.AccessLevel = menuItemOrNull.IsLocked ? menuItemOrNull.AccessLevel : formModel.AccessLevel;
            menuItemOrNull.IsActive = menuItemOrNull.IsLocked || formModel.IsActive;
            menuItemOrNull.IsVisibleToAllTeams = MenuAccessLevelUtility.IsTeamScoped(menuItemOrNull.AccessLevel) && formModel.IsVisibleToAllTeams;
            dbContext.NavigationMenuTeamPermissions.RemoveRange(menuItemOrNull.TeamPermissions);
            if (MenuAccessLevelUtility.IsTeamScoped(menuItemOrNull.AccessLevel) && menuItemOrNull.IsVisibleToAllTeams == false)
            {
                List<int> activeTeamIDs = await dbContext.Teams.Where(team => team.IsActive && formModel.TeamIDs.Contains(team.ID)).Select(team => team.ID).ToListAsync(cancellationToken);
                foreach (int teamID in activeTeamIDs.Distinct())
                {
                    NavigationMenuTeamPermission permission = new NavigationMenuTeamPermission();
                    permission.NavigationMenuItemID = menuItemOrNull.ID;
                    permission.TeamID = teamID;
                    dbContext.NavigationMenuTeamPermissions.Add(permission);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult.Succeed();
        }
    }

    public async Task<bool> CanAccessPathAsync(UserSession userSession, string path, CancellationToken cancellationToken)
    {
        string routePath = normalizePath(path);
        if (shouldBypassMenuGuard(routePath))
        {
            return true;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.TeamPermissions).ThenInclude(menuPermission => menuPermission.Team).OrderByDescending(menuItem => menuItem.Href.Length).ToListAsync(cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (isPathMatch(routePath, menuItem.Href))
                {
                    bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
                    return await isVisibleForUserAsync(menuItem, userSession, usesConfiguredPermissions, cancellationToken);
                }
            }
        }

        return true;
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

            int? targetTeamIDOrNull = MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel) ? userSession.TeamID : null;
            foreach (string permissionKey in permissionKeys)
            {
                bool hasPermission = await mOrganizationPermissionService.HasPermissionAsync(userSession, permissionKey, targetTeamIDOrNull, cancellationToken);
                if (hasPermission)
                {
                    return true;
                }
            }

            return false;
        }

        return MenuAccessLevelUtility.IsRoleAllowed(menuItem.AccessLevel, userSession.Role);
    }

    private IReadOnlyList<string> getMenuPermissionKeys(NavigationMenuItem menuItem)
    {
        switch (menuItem.MenuKey)
        {
            case "Dashboard":
                return new List<string>();
            case "Calendar":
                return new[] { PermissionKeys.CALENDAR_READ };
            case "Todos":
                return new[] { PermissionKeys.TODOS_READ };
            case "ApprovalRequest":
                return new[] { PermissionKeys.APPROVAL_REQUEST };
            case "ApprovalInbox":
                return new[] { PermissionKeys.APPROVAL_APPROVE, PermissionKeys.APPROVAL_FINAL_APPROVE };
            case "ApprovalHistory":
                return new[] { PermissionKeys.APPROVAL_HISTORY_READ };
            case "AdminUsers":
                return new[] { PermissionKeys.USERS_MANAGE };
            case "AdminTeams":
                return new[] { PermissionKeys.ORGANIZATION_MANAGE };
            case "AdminPositions":
                return new[] { PermissionKeys.POSITIONS_MANAGE, PermissionKeys.PERMISSIONS_MANAGE };
            case "AdminApprovalWorkflows":
                return new[] { PermissionKeys.APPROVAL_WORKFLOWS_MANAGE };
            case "AdminSettings":
                return new[] { PermissionKeys.SYSTEM_SETTINGS_MANAGE };
            case "AdminGoogle":
                return new[] { PermissionKeys.GOOGLE_CONNECTION_MANAGE };
            case "AdminMenus":
                return new[] { PermissionKeys.MENUS_MANAGE };
            case "AdminCalendarAudit":
                return new[] { PermissionKeys.CALENDAR_AUDIT_READ };
            case "Upload":
                return new[] { PermissionKeys.FILES_UPLOAD };
            case "Materials":
            case "ManagementFiles":
            case "Common":
            case "Public":
            case "ResponsibleResources":
            case "ApprovedFiles":
            case "WorkGuidelines":
            case "WorkResources":
                return new[] { PermissionKeys.FILES_READ };
            default:
                return new[] { PermissionKeys.MENU_READ };
        }
    }

    private int getVisibleMenuGroupOrder(NavigationMenuItem menuItem)
    {
        switch (menuItem.MenuKey)
        {
            case "Dashboard":
                return 10;
            case "Calendar":
            case "Todos":
            case "WorkGuidelines":
            case "WorkResources":
                return 20;
            case "Materials":
            case "ManagementFiles":
            case "Common":
            case "Public":
            case "Upload":
            case "ResponsibleResources":
                return 30;
            case "ApprovalRequest":
            case "ApprovalInbox":
            case "ApprovalHistory":
            case "ApprovedFiles":
                return 40;
            default:
                break;
        }

        switch (menuItem.AccessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return 20;
            case EMenuAccessLevel.TeamMember:
                return 30;
            case EMenuAccessLevel.TeamLeader:
                return 30;
            case EMenuAccessLevel.ManagementManager:
                return 50;
            case EMenuAccessLevel.Management:
                return 50;
            case EMenuAccessLevel.Admin:
                return 50;
            default:
                return 90;
        }
    }

    private string normalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/dashboard";
        }

        string normalizedPath = path.Trim();
        if (normalizedPath.EndsWith("/") && normalizedPath.Length > 1)
        {
            normalizedPath = normalizedPath.TrimEnd('/');
        }

        return normalizedPath;
    }

    private bool shouldBypassMenuGuard(string routePath)
    {
        return routePath == "/login" || routePath == "/access-denied" || routePath == "/not-found" || routePath == "/Error" || routePath.StartsWith("/account") || routePath.StartsWith("/files");
    }

    private bool isPathMatch(string routePath, string menuHref)
    {
        string normalizedMenuHref = normalizePath(menuHref);
        return routePath == normalizedMenuHref || routePath.StartsWith(normalizedMenuHref + "/");
    }
}
