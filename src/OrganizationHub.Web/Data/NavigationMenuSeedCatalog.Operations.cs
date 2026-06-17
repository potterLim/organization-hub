using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private static void addOperationMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Dashboard",
            DisplayName = "홈",
            Href = "/dashboard",
            IconKey = "Dashboard",
            SectionKey = "Primary",
            SortOrder = 10,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Calendar",
            DisplayName = "일정",
            Href = "/calendar",
            IconKey = "CalendarMonth",
            SectionKey = "Operations",
            SortOrder = 20,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Todos",
            DisplayName = "할 일",
            Href = "/todos",
            IconKey = "TaskAlt",
            SectionKey = "Operations",
            SortOrder = 30,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "WorkGuidelines",
            DisplayName = "업무 기준",
            Href = "/work-guidelines",
            IconKey = "Rule",
            SectionKey = "Operations",
            SortOrder = 40,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "WorkResources",
            DisplayName = OrganizationHubWorkspaceDefaults.WORK_RESOURCES_FOLDER_NAME,
            Href = "/work-resources",
            IconKey = "EventAvailable",
            SectionKey = "Operations",
            SortOrder = 50,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
