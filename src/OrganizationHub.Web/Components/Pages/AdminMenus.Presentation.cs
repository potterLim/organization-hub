using System.Collections.Generic;
using MudBlazor;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Components.Pages;

public partial class AdminMenus
{
    private string getTeamPermissionText(NavigationMenuItem menuItem)
    {
        if (MenuAccessLevelUtility.IsTeamScoped(menuItem.AccessLevel) == false)
        {
            return "팀 제한 없음";
        }

        if (menuItem.IsVisibleToAllTeams)
        {
            return "모든 팀";
        }

        List<string> teamNames = new List<string>();
        foreach (NavigationMenuTeamPermission permission in menuItem.TeamPermissions)
        {
            if (permission.Team != null && permission.Team.IsActive)
            {
                teamNames.Add(permission.Team.DisplayName);
            }
        }

        if (teamNames.Count == 0)
        {
            return "팀 미선택";
        }

        return string.Join(", ", teamNames);
    }

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
}
