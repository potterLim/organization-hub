using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal static class ResourceOperationAccessUtility
{
    public static async Task<ResourceOperationAccess> ResolveAsync(
        IOrganizationPermissionService permissions,
        UserSession userSession,
        PermissionKey permissionKey,
        IReadOnlyList<OrganizationTeamId?> targetTeamIds,
        CancellationToken cancellationToken)
    {
        bool canUseOwnItems = false;
        foreach (OrganizationTeamId? targetTeamIdOrNull in targetTeamIds)
        {
            PermissionDecision allItems = await permissions.DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, null, cancellationToken);
            if (allItems.IsAllowed)
            {
                return new ResourceOperationAccess(true, true);
            }

            PermissionDecision ownItems = await permissions.DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, userSession.UserAccountId, cancellationToken);
            canUseOwnItems |= ownItems.IsAllowed;
        }

        return new ResourceOperationAccess(false, canUseOwnItems);
    }
}
