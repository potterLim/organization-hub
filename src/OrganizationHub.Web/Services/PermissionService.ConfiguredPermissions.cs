using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class PermissionService
{
    private async Task<EPermissionConfigurationMode> getPermissionConfigurationModeAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSession, cancellationToken);
        if (usesConfiguredPermissions)
        {
            return EPermissionConfigurationMode.ConfiguredPermissions;
        }

        return EPermissionConfigurationMode.FallbackPolicy;
    }

    private async Task<bool> canUseConfiguredOrFallbackPermissionAsync(
        UserSession userSession,
        PermissionKey permissionKey,
        OrganizationTeamId? targetTeamIdOrNull,
        EPermissionConfigurationMode configurationMode,
        EPermissionFallback fallbackPermission,
        CancellationToken cancellationToken)
    {
        if (configurationMode == EPermissionConfigurationMode.ConfiguredPermissions)
        {
            return await mOrganizationPermissionService.HasPermissionAsync(userSession, permissionKey, targetTeamIdOrNull, cancellationToken);
        }

        return PermissionFallbackUtility.IsAllowed(fallbackPermission);
    }

    private async Task<bool> canUseAnyConfiguredOrFallbackCalendarWritePermissionAsync(
        UserSession userSession,
        OrganizationTeamId? targetTeamIdOrNull,
        EPermissionConfigurationMode configurationMode,
        EPermissionFallback fallbackPermission,
        CancellationToken cancellationToken)
    {
        if (configurationMode == EPermissionConfigurationMode.FallbackPolicy)
        {
            return PermissionFallbackUtility.IsAllowed(fallbackPermission);
        }

        bool canCreate = await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_CREATE), targetTeamIdOrNull, cancellationToken);
        if (canCreate)
        {
            return true;
        }

        bool canUpdate = await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_UPDATE), targetTeamIdOrNull, cancellationToken);
        if (canUpdate)
        {
            return true;
        }

        return await mOrganizationPermissionService.HasPermissionAsync(userSession, new PermissionKey(PermissionKeys.CALENDAR_DELETE), targetTeamIdOrNull, cancellationToken);
    }
}
