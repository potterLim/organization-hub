using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace OrganizationHub.Web.Services;

public sealed class PermissionAuthorizationRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> PermissionKeys
    {
        get; private init;
    }
    public IReadOnlyList<string> FallbackRoleNames
    {
        get; private init;
    }

    public PermissionAuthorizationRequirement(IReadOnlyList<string> permissionKeys, IReadOnlyList<string> fallbackRoleNames)
    {
        PermissionKeys = permissionKeys;
        FallbackRoleNames = fallbackRoleNames;
    }
}
