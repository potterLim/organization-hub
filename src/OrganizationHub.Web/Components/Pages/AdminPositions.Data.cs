using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminPositions
{
    private async Task loadDataAsync()
    {
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        IReadOnlyList<PermissionDefinition> permissionDefinitions = await PositionManagementService.GetPermissionDefinitionsAsync(CancellationToken.None);
        mActiveTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.ToList();
        mPermissionDefinitions = permissionDefinitions.OrderBy(definition => definition.Category).ThenBy(definition => definition.DisplayName).ToList();
        mShouldInitializeSortable = true;
    }
}
