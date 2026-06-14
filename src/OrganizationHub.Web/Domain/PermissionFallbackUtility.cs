namespace OrganizationHub.Web.Domain;

public static class PermissionFallbackUtility
{
    public static EPermissionFallback FromDecision(bool isAllowed)
    {
        if (isAllowed)
        {
            return EPermissionFallback.Allow;
        }

        return EPermissionFallback.Deny;
    }

    public static bool IsAllowed(EPermissionFallback fallbackPermission)
    {
        return fallbackPermission == EPermissionFallback.Allow;
    }
}
