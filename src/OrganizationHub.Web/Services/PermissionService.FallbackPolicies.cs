using System;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private bool canUploadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (isManagementTeam(team))
        {
            return UserPermissionPolicy.CanUploadManagementMaterials(userSession.Role);
        }

        return UserPermissionPolicy.CanUploadTeamMaterials(userSession.Role);
    }

    private bool canReadMaterialsForTeam(UserSession userSession, Team team)
    {
        if (userSession.IsManagementRole())
        {
            return true;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool canWriteTeamWorkFallback(UserSession userSession, Team team)
    {
        if (UserPermissionPolicy.CanWriteAnyTeamWork(userSession.Role))
        {
            return true;
        }

        if (UserPermissionPolicy.CanWriteOwnTeamWork(userSession.Role) == false)
        {
            return false;
        }

        if (userSession.TeamID.HasValue == false)
        {
            return false;
        }

        return userSession.TeamID.Value == team.ID;
    }

    private bool isManagementTeam(Team team)
    {
        return string.Equals(team.TeamKey, OrganizationHubWorkspaceDefaults.MANAGEMENT_TEAM_KEY, StringComparison.OrdinalIgnoreCase);
    }
}
