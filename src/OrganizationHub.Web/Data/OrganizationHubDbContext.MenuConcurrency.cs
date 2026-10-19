using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

public sealed partial class OrganizationHubDbContext
{
    private void prepareMenuRevisions()
    {
        ChangeTracker.DetectChanges();
        int[] menuIds = ChangeTracker.Entries<NavigationMenuTeamPermission>()
            .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            .Select(entry => entry.Entity.NavigationMenuItemId).Where(id => id > 0).Distinct().ToArray();
        foreach (int menuId in menuIds)
        {
            NavigationMenuItem? itemOrNull = NavigationMenuItems.Find(menuId);
            if (itemOrNull != null && Entry(itemOrNull).State != EntityState.Added && Entry(itemOrNull).State != EntityState.Deleted)
            {
                Entry(itemOrNull).Property(item => item.Revision).IsModified = true;
            }
        }
        foreach (EntityEntry<NavigationMenuItem> entry in ChangeTracker.Entries<NavigationMenuItem>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Revision = entry.Property(item => item.Revision).OriginalValue + 1;
            }
        }
        foreach (EntityEntry<NavigationMenuSection> entry in ChangeTracker.Entries<NavigationMenuSection>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Revision = entry.Property(section => section.Revision).OriginalValue + 1;
            }
        }
    }
}
