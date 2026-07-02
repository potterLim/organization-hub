using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private const int DASHBOARD_MENU_SORT_ORDER = 10;
    private const int CALENDAR_MENU_SORT_ORDER = 20;
    private const int TODOS_MENU_SORT_ORDER = 30;
    private const int WORK_GUIDELINES_MENU_SORT_ORDER = 40;
    private const int WORK_RESOURCES_MENU_SORT_ORDER = 50;

    private static void addOperationMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.DASHBOARD,
            DisplayName = "홈",
            Href = "/dashboard",
            IconKey = "Dashboard",
            SectionKey = PRIMARY_SECTION_KEY,
            SortOrder = DASHBOARD_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.CALENDAR,
            DisplayName = "일정",
            Href = "/calendar",
            IconKey = "CalendarMonth",
            SectionKey = OPERATIONS_SECTION_KEY,
            SortOrder = CALENDAR_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.TODOS,
            DisplayName = "할 일",
            Href = "/todos",
            IconKey = "TaskAlt",
            SectionKey = OPERATIONS_SECTION_KEY,
            SortOrder = TODOS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.WORK_GUIDELINES,
            DisplayName = "업무 기준",
            Href = "/work-guidelines",
            IconKey = "Rule",
            SectionKey = OPERATIONS_SECTION_KEY,
            SortOrder = WORK_GUIDELINES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.WORK_RESOURCES,
            DisplayName = OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME,
            Href = "/work-resources",
            IconKey = "EventAvailable",
            SectionKey = OPERATIONS_SECTION_KEY,
            SortOrder = WORK_RESOURCES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
