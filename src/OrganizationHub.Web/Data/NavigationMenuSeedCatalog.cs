using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    internal static List<NavigationMenuItem> createDefaultMenuItems()
    {
        List<NavigationMenuSeedDefinition> menuDefinitions = createDefaultMenuDefinitions();
        List<NavigationMenuItem> menuItems = new List<NavigationMenuItem>();
        foreach (NavigationMenuSeedDefinition menuDefinition in menuDefinitions)
        {
            menuItems.Add(createMenuItem(menuDefinition));
        }

        return menuItems;
    }

    private static List<NavigationMenuSeedDefinition> createDefaultMenuDefinitions()
    {
        List<NavigationMenuSeedDefinition> menuDefinitions = new List<NavigationMenuSeedDefinition>();
        addOperationMenuDefinitions(menuDefinitions);
        addResourceMenuDefinitions(menuDefinitions);
        addApprovalMenuDefinitions(menuDefinitions);
        addAdministrationMenuDefinitions(menuDefinitions);
        return menuDefinitions;
    }

    private static NavigationMenuItem createMenuItem(NavigationMenuSeedDefinition definition)
    {
        NavigationMenuItem menuItem = new NavigationMenuItem();
        menuItem.MenuKey = definition.MenuKey;
        menuItem.DisplayName = definition.DisplayName;
        menuItem.Href = definition.Href;
        menuItem.IconKey = definition.IconKey;
        menuItem.SortOrder = definition.SortOrder;
        menuItem.AccessLevel = definition.AccessLevel;
        menuItem.IsActive = definition.ActivationState == EActivationState.Active;
        menuItem.IsVisibleToAllTeams = definition.TeamVisibility == EMenuTeamVisibility.AllTeams;
        menuItem.IsLocked = definition.LockState == ENavigationMenuLockState.Locked;
        return menuItem;
    }

    private sealed class NavigationMenuSeedDefinition
    {
        public required string MenuKey
        {
            get; init;
        }

        public required string DisplayName
        {
            get; init;
        }

        public required string Href
        {
            get; init;
        }

        public required string IconKey
        {
            get; init;
        }

        public required int SortOrder
        {
            get; init;
        }

        public required EMenuAccessLevel AccessLevel
        {
            get; init;
        }

        public required EActivationState ActivationState
        {
            get; init;
        }

        public required EMenuTeamVisibility TeamVisibility
        {
            get; init;
        }

        public required ENavigationMenuLockState LockState
        {
            get; init;
        }
    }
}
