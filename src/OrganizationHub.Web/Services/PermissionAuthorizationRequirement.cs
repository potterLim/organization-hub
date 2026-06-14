using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed class PermissionAuthorizationRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<PermissionKey> PermissionKeys
    {
        get; private init;
    }
    public IReadOnlyList<string> FallbackRoleNames
    {
        get; private init;
    }

    public PermissionAuthorizationRequirement(IReadOnlyList<PermissionKey> permissionKeys, IReadOnlyList<string> fallbackRoleNames)
    {
        PermissionKeys = permissionKeys;
        FallbackRoleNames = fallbackRoleNames;
    }
}
