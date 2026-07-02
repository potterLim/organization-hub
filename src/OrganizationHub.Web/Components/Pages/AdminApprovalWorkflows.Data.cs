using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminApprovalWorkflows
{
    private async Task loadDataAsync()
    {
        IReadOnlyList<Team> teams = await TeamManagementService.GetTeamsAsync(CancellationToken.None);
        IReadOnlyList<OrganizationPosition> positions = await PositionManagementService.GetPositionsAsync(CancellationToken.None);
        IReadOnlyList<UserAccount> users = await ApprovalWorkflowManagementService.GetApproverUsersAsync(CancellationToken.None);
        IReadOnlyList<PermissionDefinition> permissionDefinitions = await PositionManagementService.GetPermissionDefinitionsAsync(CancellationToken.None);
        IReadOnlyList<ApprovalWorkflow> workflows = await ApprovalWorkflowManagementService.GetWorkflowsAsync(CancellationToken.None);
        mTeams = teams.Where(team => team.IsActive).OrderBy(team => team.SortOrder).ThenBy(team => team.DisplayName).ToList();
        mPositions = positions.Where(position => position.IsActive).ToList();
        mUsers = users.ToList();
        mPermissionDefinitions = permissionDefinitions.OrderBy(definition => definition.Category).ThenBy(definition => definition.DisplayName).ToList();
        mWorkflows = workflows.ToList();
        mShouldInitializeSortable = true;
    }
}
