using System;

namespace OrganizationHub.Web.Domain;

public static class PermissionScopeSupport
{
    public static bool IsSupported(EPermissionScopeType scope)
    {
        return Enum.IsDefined(scope) && scope != EPermissionScopeType.OwnTeamAndChildren;
    }

    public static EPermissionScopeType GetEditableScope(EPermissionScopeType scope)
    {
        return scope == EPermissionScopeType.OwnTeamAndChildren ? EPermissionScopeType.OwnTeam : scope;
    }
}
