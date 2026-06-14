using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private static void addAdministrationMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminUsers",
            DisplayName = "사용자 관리",
            Href = "/admin/users",
            IconKey = "ManageAccounts",
            SortOrder = 300,
            AccessLevel = EMenuAccessLevel.ManagementManager,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminTeams",
            DisplayName = "팀 관리",
            Href = "/admin/teams",
            IconKey = "Groups2",
            SortOrder = 310,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminPositions",
            DisplayName = "직책과 권한 설정",
            Href = "/admin/positions",
            IconKey = "AdminPanelSettings",
            SortOrder = 320,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminApprovalWorkflows",
            DisplayName = "결재 경로 설정",
            Href = "/admin/approval-workflows",
            IconKey = "AccountTree",
            SortOrder = 330,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminSettings",
            DisplayName = "기본 설정",
            Href = "/admin/settings",
            IconKey = "Settings",
            SortOrder = 340,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminMenus",
            DisplayName = "메뉴 보이기 설정",
            Href = "/admin/menus",
            IconKey = "Menu",
            SortOrder = 350,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminGoogle",
            DisplayName = "Google 연결 설정",
            Href = "/admin/google",
            IconKey = "CloudSync",
            SortOrder = 360,
            AccessLevel = EMenuAccessLevel.Admin,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Locked,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "AdminCalendarAudit",
            DisplayName = "일정 삭제 기록",
            Href = "/admin/calendar-audit",
            IconKey = "History",
            SortOrder = 370,
            AccessLevel = EMenuAccessLevel.ManagementManager,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.SelectedTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
