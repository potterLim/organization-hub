using System.Collections.Generic;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Data;

internal static partial class NavigationMenuSeedCatalog
{
    private static void addApprovalMenuDefinitions(List<NavigationMenuSeedDefinition> menuDefinitions)
    {
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ApprovalRequest",
            DisplayName = "결재 요청",
            Href = "/approvals/request",
            IconKey = "Approval",
            SortOrder = 200,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ApprovalInbox",
            DisplayName = "내 결재함",
            Href = "/approvals/inbox",
            IconKey = "FactCheck",
            SortOrder = 210,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ApprovalHistory",
            DisplayName = "결재 기록",
            Href = "/approvals/history",
            IconKey = "HistoryEdu",
            SortOrder = 220,
            AccessLevel = EMenuAccessLevel.Authenticated,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
        menuDefinitions.Add(new NavigationMenuSeedDefinition
        {
            MenuKey = "ApprovedFiles",
            DisplayName = "결재 완료 자료",
            Href = "/approved-files",
            IconKey = "Verified",
            SortOrder = 230,
            AccessLevel = EMenuAccessLevel.TeamMember,
            ActivationState = EActivationState.Active,
            TeamVisibility = EMenuTeamVisibility.AllTeams,
            LockState = ENavigationMenuLockState.Editable,
        });
    }
}
