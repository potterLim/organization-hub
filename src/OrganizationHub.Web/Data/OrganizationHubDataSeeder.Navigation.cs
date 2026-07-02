using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDataSeeder
{
    private async Task seedNavigationMenusAsync(OrganizationHubDbContext dbContext, CancellationToken cancellationToken)
    {
        Dictionary<string, NavigationMenuSection> sectionsByKey = await ensureNavigationMenuSectionsAsync(dbContext, cancellationToken);
        List<NavigationMenuItem> defaultMenuItems = NavigationMenuSeedCatalog.createDefaultMenuItems(sectionsByKey);
        List<NavigationMenuItem> existingMenuItems = await dbContext.NavigationMenuItems.Include(menuItem => menuItem.Section).ToListAsync(cancellationToken);
        NavigationMenuSeedUpgradePolicy.DeactivateRetiredMenus(existingMenuItems);
        foreach (NavigationMenuItem defaultMenuItem in defaultMenuItems)
        {
            NavigationMenuItem? existingMenuItemOrNull = existingMenuItems.FirstOrDefault(menuItem => menuItem.MenuKey == defaultMenuItem.MenuKey);
            if (existingMenuItemOrNull == null)
            {
                dbContext.NavigationMenuItems.Add(defaultMenuItem);
                continue;
            }

            existingMenuItemOrNull.Href = defaultMenuItem.Href;
            existingMenuItemOrNull.IconKey = defaultMenuItem.IconKey;
            existingMenuItemOrNull.IsLocked = defaultMenuItem.IsLocked;
            if (string.IsNullOrWhiteSpace(existingMenuItemOrNull.RequiredPermissionKeys) || existingMenuItemOrNull.IsLocked)
            {
                existingMenuItemOrNull.RequiredPermissionKeys = defaultMenuItem.RequiredPermissionKeys;
            }

            if (existingMenuItemOrNull.SectionId <= 0 || existingMenuItemOrNull.Section == null)
            {
                existingMenuItemOrNull.Section = defaultMenuItem.Section;
            }

            if (existingMenuItemOrNull.IsLocked)
            {
                existingMenuItemOrNull.AccessLevel = defaultMenuItem.AccessLevel;
                existingMenuItemOrNull.IsActive = true;
            }

            NavigationMenuSeedUpgradePolicy.ApplyStoredMenuUpgrades(existingMenuItemOrNull, defaultMenuItem);
        }
    }

    private async Task<Dictionary<string, NavigationMenuSection>> ensureNavigationMenuSectionsAsync(
        OrganizationHubDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        List<NavigationMenuSection> defaultSections = NavigationMenuSeedCatalog.createDefaultMenuSections();
        List<NavigationMenuSection> existingSections = await dbContext.NavigationMenuSections.ToListAsync(cancellationToken);
        foreach (NavigationMenuSection defaultSection in defaultSections)
        {
            NavigationMenuSection? existingSectionOrNull = existingSections.FirstOrDefault(section => section.SectionKey == defaultSection.SectionKey);
            if (existingSectionOrNull == null)
            {
                dbContext.NavigationMenuSections.Add(defaultSection);
                existingSections.Add(defaultSection);
            }
            else if (string.IsNullOrWhiteSpace(existingSectionOrNull.DisplayName))
            {
                existingSectionOrNull.DisplayName = defaultSection.DisplayName;
            }
        }

        return existingSections.ToDictionary(section => section.SectionKey);
    }
}
