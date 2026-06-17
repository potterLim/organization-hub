using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    internal static List<NavigationMenuSection> createDefaultMenuSections()
    {
        return new List<NavigationMenuSection>
        {
            createMenuSection("Primary", "기본", 10),
            createMenuSection("Operations", "운영", 20),
            createMenuSection("Resources", "자료실", 30),
            createMenuSection("Approvals", "결재", 40),
            createMenuSection("Administration", "관리", 50)
        };
    }

    internal static List<NavigationMenuItem> createDefaultMenuItems(IReadOnlyDictionary<string, NavigationMenuSection> sectionsByKey)
    {
        List<NavigationMenuSeedDefinition> menuDefinitions = createDefaultMenuDefinitions();
        List<NavigationMenuItem> menuItems = new List<NavigationMenuItem>();
        foreach (NavigationMenuSeedDefinition menuDefinition in menuDefinitions)
        {
            menuItems.Add(createMenuItem(menuDefinition, sectionsByKey));
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

    private static NavigationMenuSection createMenuSection(string sectionKey, string displayName, int sortOrder)
    {
        NavigationMenuSection section = new NavigationMenuSection();
        section.SectionKey = sectionKey;
        section.DisplayName = displayName;
        section.SortOrder = sortOrder;
        return section;
    }

    private static NavigationMenuItem createMenuItem(NavigationMenuSeedDefinition definition, IReadOnlyDictionary<string, NavigationMenuSection> sectionsByKey)
    {
        NavigationMenuItem menuItem = new NavigationMenuItem();
        menuItem.MenuKey = definition.MenuKey;
        menuItem.DisplayName = definition.DisplayName;
        menuItem.Href = definition.Href;
        menuItem.IconKey = definition.IconKey;
        menuItem.Section = sectionsByKey[definition.SectionKey];
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

        public required string SectionKey
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
