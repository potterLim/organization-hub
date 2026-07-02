using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Data;

internal static partial class OrganizationHubModelConfiguration
{
    private static void configureNavigation(ModelBuilder modelBuilder)
    {
        configureNavigationMenuSections(modelBuilder);
        configureNavigationMenuItems(modelBuilder);
        configureNavigationMenuTeamPermissions(modelBuilder);
    }

    private static void configureNavigationMenuSections(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<NavigationMenuSection> sectionBuilder = modelBuilder.Entity<NavigationMenuSection>();
        sectionBuilder.HasKey(section => section.Id);
        sectionBuilder.HasIndex(section => section.SectionKey).IsUnique();
        sectionBuilder.Property(section => section.SectionKey).HasMaxLength(64).IsRequired();
        sectionBuilder.Property(section => section.DisplayName).HasMaxLength(40).IsRequired();
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
        menuBuilder.Property(menuItem => menuItem.RequiredPermissionKeys).HasMaxLength(512).IsRequired();
        menuBuilder.Property(menuItem => menuItem.AccessLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
        menuBuilder
            .HasOne(menuItem => menuItem.Section)
            .WithMany(section => section.MenuItems)
            .HasForeignKey(menuItem => menuItem.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
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
