using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private static void addResourceMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Materials",
            DisplayName = "팀 자료",
            Href = "/materials",
            IconKey = "Folder",
            SectionKey = "Resources",
            SortOrder = 100,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ResponsibleResources",
            DisplayName = OrganizationHubWorkspaceDefaults.RESPONSIBLE_RESOURCES_FOLDER_NAME,
            Href = "/responsible-resources",
            IconKey = "SupervisorAccount",
            SectionKey = "Resources",
            SortOrder = 110,
            AccessLevel = EMenuAccessLevel.TeamLeader,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ManagementFiles",
            DisplayName = OrganizationHubWorkspaceDefaults.MANAGEMENT_FOLDER_NAME,
            Href = "/management-files",
            IconKey = "Business",
            SectionKey = "Resources",
            SortOrder = 120,
            AccessLevel = EMenuAccessLevel.Management,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Common",
            DisplayName = OrganizationHubWorkspaceDefaults.COMMON_FOLDER_NAME,
            Href = "/common",
            IconKey = "Groups",
            SectionKey = "Resources",
            SortOrder = 130,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Public",
            DisplayName = OrganizationHubWorkspaceDefaults.PUBLIC_FOLDER_NAME,
            Href = "/public-files",
            IconKey = "Public",
            SectionKey = "Resources",
            SortOrder = 140,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "Upload",
            DisplayName = "파일 올리기",
            Href = "/upload",
            IconKey = "UploadFile",
            SectionKey = "Resources",
            SortOrder = 150,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
