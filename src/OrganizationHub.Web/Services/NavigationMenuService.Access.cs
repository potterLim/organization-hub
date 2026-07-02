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

internal sealed partial class NavigationMenuService
{
    public async Task<bool> CanAccessPathAsync(UserSession userSession, NavigationPath path, CancellationToken cancellationToken)
    {
        NavigationPath routePath = getMenuGuardPath(path);
        if (shouldBypassMenuGuard(routePath))
        {
            return true;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.Section)
                .Include(menuItem => menuItem.TeamPermissions)
                .ThenInclude(menuPermission => menuPermission.Team)
                .OrderByDescending(menuItem => menuItem.Href.Length)
                .ToListAsync(cancellationToken);
            NavigationMenuVisibilityEvaluationContext evaluationContext =
                await createVisibilityEvaluationContextAsync(userSession, cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (isPathMatch(routePath, NavigationPath.CreateOptional(menuItem.Href)))
                {
                    return await canUseMenuItemAsync(menuItem, evaluationContext, cancellationToken);
                }
            }
        }

        return false;
    }

    private NavigationPath getMenuGuardPath(NavigationPath routePath)
    {
        if (routePath.GetRequiredValue() == "/")
        {
            return NavigationPath.Dashboard;
        }

        return routePath;
    }

    private bool shouldBypassMenuGuard(NavigationPath routePath)
    {
        string routePathValue = routePath.GetRequiredValue();
        return
            routePathValue == "/login" ||
            routePathValue == "/access-denied" ||
            routePathValue == "/not-found" ||
            routePathValue == "/Error" ||
            routePathValue.StartsWith("/account", StringComparison.Ordinal) ||
            routePathValue.StartsWith("/files", StringComparison.Ordinal);
    }

    private bool isPathMatch(NavigationPath routePath, NavigationPath menuHref)
    {
        string routePathValue = routePath.GetRequiredValue();
        string menuHrefValue = menuHref.GetRequiredValue();
        return routePathValue == menuHrefValue || routePathValue.StartsWith(menuHrefValue + "/", StringComparison.Ordinal);
    }
}
