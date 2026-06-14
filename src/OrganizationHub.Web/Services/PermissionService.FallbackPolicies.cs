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

        if (userSession.HasTeam == false)
        {
            return false;
        }

        OrganizationTeamId userTeamId = userSession.GetRequiredOrganizationTeamId();
        OrganizationTeamId materialTeamId = OrganizationTeamId.CreateExisting(team.Id);
        return isSameTeam(userTeamId, materialTeamId);
    }

    private bool canWriteTeamWorkFallback(UserSession userSession, Team team)
    {
        PermissionDecision decision = UserPermissionPolicy.DecideCalendarWrite(
            userSession.Role,
            ECalendarViewKind.Team,
            OrganizationTeamId.CreateExisting(team.Id),
            createTeamIdOrNull(userSession));
        return decision.IsAllowed;
    }

    private bool isManagementTeam(Team team)
    {
        return new OrganizationTeamKey(team.TeamKey).IsManagementTeamKey;
    }

    private static bool isSameTeam(OrganizationTeamId firstTeamId, OrganizationTeamId secondTeamId)
    {
        return firstTeamId.HasValue && secondTeamId.HasValue && firstTeamId == secondTeamId;
    }

    private static OrganizationTeamId? createTeamIdOrNull(UserSession userSession)
    {
        if (userSession.HasTeam == false)
        {
            return null;
        }

        return userSession.GetRequiredOrganizationTeamId();
    }
}
