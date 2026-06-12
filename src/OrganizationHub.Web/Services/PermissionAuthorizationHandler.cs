using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
            foreach (string permissionKey in requirement.PermissionKeys)
            {
                bool hasPermission = await mOrganizationPermissionService.HasPermissionAsync(userSessionOrNull, permissionKey, null, CancellationToken.None);
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
}
