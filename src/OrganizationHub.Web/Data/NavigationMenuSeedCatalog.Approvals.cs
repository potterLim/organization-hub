using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private const int APPROVAL_REQUEST_MENU_SORT_ORDER = 200;
    private const int APPROVAL_INBOX_MENU_SORT_ORDER = 210;
    private const int APPROVAL_HISTORY_MENU_SORT_ORDER = 220;
    private const int APPROVED_FILES_MENU_SORT_ORDER = 230;

    private static void addApprovalMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.APPROVAL_REQUEST,
            DisplayName = "결재 요청",
            Href = "/approvals/request",
            IconKey = "Approval",
            SectionKey = APPROVALS_SECTION_KEY,
            SortOrder = APPROVAL_REQUEST_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.APPROVAL_INBOX,
            DisplayName = "내 결재함",
            Href = "/approvals/inbox",
            IconKey = "FactCheck",
            SectionKey = APPROVALS_SECTION_KEY,
            SortOrder = APPROVAL_INBOX_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.APPROVAL_HISTORY,
            DisplayName = "결재 기록",
            Href = "/approvals/history",
            IconKey = "HistoryEdu",
            SectionKey = APPROVALS_SECTION_KEY,
            SortOrder = APPROVAL_HISTORY_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = NavigationMenuKey.APPROVED_FILES,
            DisplayName = "결재 완료 자료",
            Href = "/approved-files",
            IconKey = "Verified",
            SectionKey = APPROVALS_SECTION_KEY,
            SortOrder = APPROVED_FILES_MENU_SORT_ORDER,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
