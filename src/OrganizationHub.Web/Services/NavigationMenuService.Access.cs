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
    public async Task<bool> CanAccessPathAsync(UserSession userSession, string path, CancellationToken cancellationToken)
    {
        string routePath = normalizePath(path);
        if (shouldBypassMenuGuard(routePath))
        {
            return true;
        }

        await using (OrganizationHubDbContext dbContext = await mDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            List<NavigationMenuItem> menuItems = await dbContext.NavigationMenuItems
                .Include(menuItem => menuItem.TeamPermissions)
                .ThenInclude(menuPermission => menuPermission.Team)
                .OrderByDescending(menuItem => menuItem.Href.Length)
                .ToListAsync(cancellationToken);
            foreach (NavigationMenuItem menuItem in menuItems)
            {
                if (isPathMatch(routePath, menuItem.Href))
                {
                    EPermissionConfigurationMode configurationMode = await getNavigationPermissionConfigurationModeAsync(userSession, cancellationToken);
                    return await isVisibleForUserAsync(menuItem, userSession, configurationMode, cancellationToken);
                }
            }
        }

        return true;
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
        return
            routePath == "/login" ||
            routePath == "/access-denied" ||
            routePath == "/not-found" ||
            routePath == "/Error" ||
            routePath.StartsWith("/account") ||
            routePath.StartsWith("/files");
    }

    private bool isPathMatch(string routePath, string menuHref)
    {
        string normalizedMenuHref = normalizePath(menuHref);
        return routePath == normalizedMenuHref || routePath.StartsWith(normalizedMenuHref + "/");
    }
}
