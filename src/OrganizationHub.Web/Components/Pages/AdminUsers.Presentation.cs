using System;
using System.Collections.Generic;
using System.Linq;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminUsers
{
    private void showOperationResult(ServiceResult operationResult, string successMessage)
    {
        if (operationResult.IsSuccess)
        {
            mMessage = successMessage;
            mMessageSeverity = Severity.Success;
            return;
        }

        mMessage = operationResult.ErrorMessage;
        mMessageSeverity = Severity.Error;
    }

    private string getAffiliationName(UserAccount userAccount)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return "시스템";
        }

        if (UserRoleUtility.GetAffiliation(userAccount.Role) == EUserAffiliation.Management)
        {
            return OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_DISPLAY_NAME;
        }

        if (userAccount.Team == null)
        {
            return "팀";
        }

        return userAccount.Team.DisplayName;
    }

    private string getPositionName(UserAccount userAccount)
    {
        if (userAccount.Position != null)
        {
            return userAccount.Position.Name;
        }

        return UserRoleUtility.GetDisplayName(userAccount.Role);
    }

    private bool isSystemAdministrationTeam(Team team)
    {
        return team.TeamKey.Equals(OrganizationHubWorkspaceDefaults.SYSTEM_ADMINISTRATION_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<Team> getSelectableTeams()
    {
        return mTeams.Where(team => isSystemAdministrationTeam(team) == false).ToList();
    }

    private IReadOnlyList<OrganizationPosition> getSelectablePositions()
    {
        if (mFormModel.TeamId.HasValue == false)
        {
            return new List<OrganizationPosition>();
        }

        List<OrganizationPosition> positions = mPositions
            .Where(position => position.TeamId == mFormModel.TeamId.Value && position.IsSystemAdministrator == false)
            .OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name)
            .ToList();
        return positions;
    }

    private int? getAdministrationTeamIdOrNull()
    {
        Team? administrationTeamOrNull = mTeams.FirstOrDefault(team => isSystemAdministrationTeam(team));
        if (administrationTeamOrNull == null)
        {
            return null;
        }

        return administrationTeamOrNull.Id;
    }

    private EUserRole getDefaultOrganizationMemberRole()
    {
        return EUserRole.TeamUser;
    }

    private bool canManageUser(UserAccount userAccount)
    {
        if (userAccount.Role == EUserRole.Admin)
        {
            return mCanManageSystemAdmins;
        }

        return true;
    }
}
