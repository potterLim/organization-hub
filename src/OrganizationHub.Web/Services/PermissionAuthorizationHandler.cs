using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public PermissionAuthorizationHandler(IOrganizationPermissionService organizationPermissionService)
    {
        mOrganizationPermissionService = organizationPermissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionAuthorizationRequirement requirement)
    {
        UserSession? userSessionOrNull = UserSessionFactory.CreateOrNull(context.User);
        if (userSessionOrNull == null)
        {
            return;
        }

        bool usesConfiguredPermissions = await mOrganizationPermissionService.UsesConfiguredPermissionsAsync(userSessionOrNull, CancellationToken.None);
        if (usesConfiguredPermissions)
        {
            OrganizationTeamId? targetTeamIdOrNull = getTargetTeamIdOrNull(requirement, userSessionOrNull);
            foreach (PermissionKey permissionKey in requirement.PermissionKeys)
            {
                bool hasPermission = await mOrganizationPermissionService.HasPermissionAsync(userSessionOrNull, permissionKey, targetTeamIdOrNull, CancellationToken.None);
                if (hasPermission)
                {
                    context.Succeed(requirement);
                    return;
                }
            }

            return;
        }

        foreach (string roleName in requirement.FallbackRoleNames)
        {
            if (context.User.IsInRole(roleName))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    private static OrganizationTeamId? getTargetTeamIdOrNull(PermissionAuthorizationRequirement requirement, UserSession userSession)
    {
        switch (requirement.Target)
        {
            case EPermissionAuthorizationTarget.Global:
                return null;
            case EPermissionAuthorizationTarget.UserTeam:
                return userSession.HasTeam ? userSession.GetRequiredOrganizationTeamId() : null;
            default:
                return null;
        }
    }
}
