using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private const int PRIMARY_SECTION_SORT_ORDER = 10;
    private const int OPERATIONS_SECTION_SORT_ORDER = 20;
    private const int RESOURCES_SECTION_SORT_ORDER = 30;
    private const int APPROVALS_SECTION_SORT_ORDER = 40;
    private const int ADMINISTRATION_SECTION_SORT_ORDER = 50;

    private const string PRIMARY_SECTION_KEY = "Primary";
    private const string OPERATIONS_SECTION_KEY = "Operations";
    private const string RESOURCES_SECTION_KEY = "Resources";
    private const string APPROVALS_SECTION_KEY = "Approvals";
    private const string ADMINISTRATION_SECTION_KEY = "Administration";

    internal static List<NavigationMenuSection> createDefaultMenuSections()
    {
        List<NavigationMenuSectionSeedDefinition> sectionDefinitions = new List<NavigationMenuSectionSeedDefinition>
        {
            new NavigationMenuSectionSeedDefinition { SectionKey = PRIMARY_SECTION_KEY, DisplayName = "기본", SortOrder = PRIMARY_SECTION_SORT_ORDER },
            new NavigationMenuSectionSeedDefinition { SectionKey = OPERATIONS_SECTION_KEY, DisplayName = "운영", SortOrder = OPERATIONS_SECTION_SORT_ORDER },
            new NavigationMenuSectionSeedDefinition { SectionKey = RESOURCES_SECTION_KEY, DisplayName = "자료실", SortOrder = RESOURCES_SECTION_SORT_ORDER },
            new NavigationMenuSectionSeedDefinition { SectionKey = APPROVALS_SECTION_KEY, DisplayName = "결재", SortOrder = APPROVALS_SECTION_SORT_ORDER },
            new NavigationMenuSectionSeedDefinition { SectionKey = ADMINISTRATION_SECTION_KEY, DisplayName = "관리", SortOrder = ADMINISTRATION_SECTION_SORT_ORDER }
        };

        List<NavigationMenuSection> sections = new List<NavigationMenuSection>();
        foreach (NavigationMenuSectionSeedDefinition sectionDefinition in sectionDefinitions)
        {
            sections.Add(createMenuSection(sectionDefinition));
        }

        return sections;
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

    private static NavigationMenuSection createMenuSection(NavigationMenuSectionSeedDefinition definition)
    {
        NavigationMenuSection section = new NavigationMenuSection();
        section.SectionKey = definition.SectionKey;
        section.DisplayName = definition.DisplayName;
        section.SortOrder = definition.SortOrder;
        return section;
    }

    private static NavigationMenuItem createMenuItem(NavigationMenuSeedDefinition definition, IReadOnlyDictionary<string, NavigationMenuSection> sectionsByKey)
    {
        NavigationMenuItem menuItem = new NavigationMenuItem();
        menuItem.MenuKey = definition.MenuKey;
        menuItem.DisplayName = definition.DisplayName;
        menuItem.Href = definition.Href;
        menuItem.IconKey = definition.IconKey;
        menuItem.RequiredPermissionKeys = NavigationMenuPermissionKeys.CreateStorageValue(
            NavigationMenuPermissionCatalog.GetRequiredPermissionKeys(NavigationMenuKey.CreateRequired(definition.MenuKey)));
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

    private sealed class NavigationMenuSectionSeedDefinition
    {
        public required string SectionKey
        {
            get; init;
        }

        public required string DisplayName
        {
            get; init;
        }

        public required int SortOrder
        {
            get; init;
        }
    }
}
