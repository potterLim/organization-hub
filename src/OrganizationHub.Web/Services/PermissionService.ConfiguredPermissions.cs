using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<bool> canUsePermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken)
    {
        return await mOrganizationPermissionService.HasPermissionAsync(userSession, permissionKey, targetTeamIdOrNull, cancellationToken);
    }

    private async Task<bool> canUseAnyCalendarWritePermissionAsync(
        UserSession userSession,
        OrganizationTeamId? targetTeamIdOrNull,
        CancellationToken cancellationToken)
    {
        bool canCreate = await canUseOwnedCalendarPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_CREATE), targetTeamIdOrNull, cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await canUseOwnedCalendarPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_UPDATE), targetTeamIdOrNull, cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await canUseOwnedCalendarPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_DELETE), targetTeamIdOrNull, cancellationToken);
    }

    private async Task<bool> canUseOwnedCalendarPermissionAsync(UserSession userSession, PermissionKey permissionKey, OrganizationTeamId? targetTeamIdOrNull, CancellationToken cancellationToken)
    {
        PermissionDecision decision = await mOrganizationPermissionService.DecidePermissionAsync(userSession, permissionKey, targetTeamIdOrNull, userSession.UserAccountId, cancellationToken);
        return decision.IsAllowed;
    }
}
