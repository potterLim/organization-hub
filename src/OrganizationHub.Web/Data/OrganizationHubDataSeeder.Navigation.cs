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
        List<NavigationMenuItem> defaultMenuItems = NavigationMenuSeedCatalog.createDefaultMenuItems();
        List<NavigationMenuItem> existingMenuItems = await dbContext.NavigationMenuItems.ToListAsync(cancellationToken);
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
            if (existingMenuItemOrNull.IsLocked)
            {
                existingMenuItemOrNull.AccessLevel = defaultMenuItem.AccessLevel;
                existingMenuItemOrNull.IsActive = true;
            }

            NavigationMenuSeedUpgradePolicy.ApplyStoredMenuUpgrades(existingMenuItemOrNull, defaultMenuItem);
        }
    }
}
