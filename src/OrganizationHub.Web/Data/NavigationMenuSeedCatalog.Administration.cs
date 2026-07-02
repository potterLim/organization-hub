using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private const int ADMIN_USERS_MENU_SORT_ORDER = 300;
    private const int ADMIN_TEAMS_MENU_SORT_ORDER = 310;
    private const int ADMIN_POSITIONS_MENU_SORT_ORDER = 320;
    private const int ADMIN_APPROVAL_WORKFLOWS_MENU_SORT_ORDER = 330;
    private const int ADMIN_SETTINGS_MENU_SORT_ORDER = 340;
    private const int ADMIN_MENUS_MENU_SORT_ORDER = 350;
    private const int ADMIN_GOOGLE_MENU_SORT_ORDER = 360;
    private const int ADMIN_CALENDAR_AUDIT_MENU_SORT_ORDER = 370;

    private static void addAdministrationMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_USERS,
            DisplayName = "사용자 관리",
            Href = "/admin/users",
            IconKey = "ManageAccounts",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_USERS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.ManagementManager,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_TEAMS,
            DisplayName = "팀 관리",
            Href = "/admin/teams",
            IconKey = "Groups2",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_TEAMS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_POSITIONS,
            DisplayName = "직책과 권한 설정",
            Href = "/admin/positions",
            IconKey = "AdminPanelSettings",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_POSITIONS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_APPROVAL_WORKFLOWS,
            DisplayName = "결재 경로 설정",
            Href = "/admin/approval-workflows",
            IconKey = "AccountTree",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_APPROVAL_WORKFLOWS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_SETTINGS,
            DisplayName = "기본 설정",
            Href = "/admin/settings",
            IconKey = "Settings",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_SETTINGS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_MENUS,
            DisplayName = "메뉴 보이기 설정",
            Href = "/admin/menus",
            IconKey = "Menu",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_MENUS_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_GOOGLE,
            DisplayName = "Google 연결 설정",
            Href = "/admin/google",
            IconKey = "CloudSync",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_GOOGLE_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.ADMIN_CALENDAR_AUDIT,
            DisplayName = "일정 삭제 기록",
            Href = "/admin/calendar-audit",
            IconKey = "History",
            SectionKey = ADMINISTRATION_SECTION_KEY,
            SortOrder = ADMIN_CALENDAR_AUDIT_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.ManagementManager,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
