using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureNavigation(ModelBuilder modelBuilder)
    {
        configureNavigationMenuItems(modelBuilder);
        configureNavigationMenuTeamPermissions(modelBuilder);
    }

    private static void configureNavigationMenuItems(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<NavigationMenuItem> menuBuilder = modelBuilder.Entity<NavigationMenuItem>();
        menuBuilder.HasKey(menuItem => menuItem.Id);
        menuBuilder.HasIndex(menuItem => menuItem.MenuKey).IsUnique();
        menuBuilder.Property(menuItem => menuItem.MenuKey).HasMaxLength(64).IsRequired();
        menuBuilder.Property(menuItem => menuItem.DisplayName).HasMaxLength(80).IsRequired();
        menuBuilder.Property(menuItem => menuItem.Href).HasMaxLength(256).IsRequired();
        menuBuilder.Property(menuItem => menuItem.IconKey).HasMaxLength(64).IsRequired();
        menuBuilder.Property(menuItem => menuItem.AccessLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
    }

    private static void configureNavigationMenuTeamPermissions(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<NavigationMenuTeamPermission> menuPermissionBuilder = modelBuilder.Entity<NavigationMenuTeamPermission>();
        menuPermissionBuilder.HasKey(menuPermission => new { menuPermission.NavigationMenuItemId, menuPermission.TeamId });
        menuPermissionBuilder
            .HasOne(menuPermission => menuPermission.NavigationMenuItem)
            .WithMany(menuItem => menuItem.TeamPermissions)
            .HasForeignKey(menuPermission => menuPermission.NavigationMenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
        menuPermissionBuilder
            .HasOne(menuPermission => menuPermission.Team)
            .WithMany(team => team.NavigationMenuPermissions)
            .HasForeignKey(menuPermission => menuPermission.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
