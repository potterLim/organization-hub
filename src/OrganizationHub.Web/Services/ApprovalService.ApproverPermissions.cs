using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private bool userHasApprovalPermission(
        UserAccount user,
        UserSession requesterSession,
        string permissionKey)
    {
        if (user.Position == null || user.Position.IsActive == false)
        {
            return false;
        }

        OrganizationTeamId? requesterTeamIdOrNull = createUserTeamIdOrNull(requesterSession);
        foreach (PositionPermissionGrant permissionGrant in user.Position.PermissionGrants)
        {
            if (permissionGrant.IsAllowed == false || permissionGrant.PermissionKey != permissionKey)
            {
                continue;
            }

            if (isApprovalPermissionGrantInScope(permissionGrant, user, requesterTeamIdOrNull))
            {
                return true;
            }
        }

        return false;
    }

    private bool isApprovalPermissionGrantInScope(
        PositionPermissionGrant permissionGrant,
        UserAccount user,
        OrganizationTeamId? requesterTeamIdOrNull)
    {
        int? requesterTeamIdValueOrNull = OrganizationTeamId.GetValueOrNull(requesterTeamIdOrNull);
        switch (permissionGrant.ScopeType)
        {
            case EPermissionScopeType.Global:
                return true;
            case EPermissionScopeType.OwnTeam:
            case EPermissionScopeType.OwnTeamAndChildren:
                return requesterTeamIdValueOrNull.HasValue && user.TeamId == requesterTeamIdValueOrNull;
            case EPermissionScopeType.SelectedTeam:
                return requesterTeamIdValueOrNull.HasValue && permissionGrant.TargetTeamId == requesterTeamIdValueOrNull;
            default:
                return false;
        }
    }
}
