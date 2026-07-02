using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService
{
    private bool hasUserApprovalPermission(
        UserAccount user,
        UserSession requesterSession,
        PermissionKey permissionKey)
    {
        return hasUserApprovalPermission(user, createUserTeamIdOrNull(requesterSession), permissionKey);
    }

    private bool hasUserApprovalPermission(
        UserAccount user,
        OrganizationTeamId? requesterTeamIdOrNull,
        PermissionKey permissionKey)
    {
        if (user.Position == null || user.Position.IsActive == false)
        {
            return false;
        }

        string requiredPermissionKey = permissionKey.GetRequiredValue();
        foreach (PositionPermissionGrant permissionGrant in user.Position.PermissionGrants)
        {
            if (permissionGrant.IsAllowed == false || permissionGrant.PermissionKey != requiredPermissionKey)
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
