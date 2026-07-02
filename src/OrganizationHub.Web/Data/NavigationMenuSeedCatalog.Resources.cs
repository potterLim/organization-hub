using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private const int MATERIALS_MENU_SORT_ORDER = 100;
    private const int RESPONSIBLE_RESOURCES_MENU_SORT_ORDER = 110;
    private const int MANAGEMENT_FILES_MENU_SORT_ORDER = 120;
    private const int COMMON_FILES_MENU_SORT_ORDER = 130;
    private const int PUBLIC_FILES_MENU_SORT_ORDER = 140;
    private const int UPLOAD_MENU_SORT_ORDER = 150;

    private static void addResourceMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.MATERIALS,
            DisplayName = "팀 자료",
            Href = "/materials",
            IconKey = "Folder",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = MATERIALS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.RESPONSIBLE_RESOURCES,
            DisplayName = OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME,
            Href = "/responsible-resources",
            IconKey = "SupervisorAccount",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = RESPONSIBLE_RESOURCES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.TeamLeader,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.MANAGEMENT_FILES,
            DisplayName = OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME,
            Href = "/management-files",
            IconKey = "Business",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = MANAGEMENT_FILES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Management,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.COMMON,
            DisplayName = OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME,
            Href = "/common",
            IconKey = "Groups",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = COMMON_FILES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.PUBLIC,
            DisplayName = OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME,
            Href = "/public-files",
            IconKey = "Public",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = PUBLIC_FILES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.UPLOAD,
            DisplayName = "파일 올리기",
            Href = "/upload",
            IconKey = "UploadFile",
            SectionKey = RESOURCES_SECTION_KEY,
            SortOrder = UPLOAD_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
