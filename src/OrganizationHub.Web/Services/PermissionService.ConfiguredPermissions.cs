using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<bool> canUseConfiguredOrFallbackPermissionAsync(UserSession userSession, string permissionKey, int? targetTeamIDOrNull, bool usesConfiguredPermissions, bool fallbackPermission, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions)
        {
            return await mOrganizationPermissionService.HasPermissionAsync(userSession, permissionKey, targetTeamIDOrNull, cancellationToken);
        }

        return fallbackPermission;
    }

    private async Task<bool> canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(UserSession userSession, int? targetTeamIDOrNull, bool usesConfiguredPermissions, bool fallbackPermission, CancellationToken cancellationToken)
    {
        if (usesConfiguredPermissions == false)
        {
            return fallbackPermission;
        }

        bool canCreate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_CREATE, targetTeamIDOrNull, cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_UPDATE, targetTeamIDOrNull, cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await mOrganizationPermissionService.HasPermissionAsync(userSession, PermissionKeys.CALENDAR_DELETE, targetTeamIDOrNull, cancellationToken);
    }
}
