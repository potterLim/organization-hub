using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private async Task loadDataAsync()
    {
        if (CurrentUserSessionOrNull == null)
        {
            return;
        }

        IReadOnlyList<UserAccount> users = await UserManagementService.GetUsersAsync(CurrentUserSessionOrNull, CancellationToken.None);
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        mUsers = users.ToList();
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.Where(position => position.IsActive).OrderBy(position => position.Team == null ? 0 : position.Team.SortOrder).ThenBy(position => position.SortOrder).ThenBy(position => position.Name).ToList();
        ensureDefaultTeamSelection();
    }
}
