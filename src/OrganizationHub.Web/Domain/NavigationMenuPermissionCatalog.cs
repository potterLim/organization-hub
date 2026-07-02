using System.Collections.Generic;

namespace OrganizationHub.Web.Domain;

public static class NavigationMenuPermissionCatalog
{
    public static IReadOnlyList<PermissionKey> GetRequiredPermissionKeys(NavigationMenuKey menuKey)
    {
        switch (menuKey.GetRequiredValue())
        {
            case NavigationMenuKey.DASHBOARD:
                return new List<PermissionKey>();
            case NavigationMenuKey.CALENDAR:
                return createPermissionKeys(PermissionKeys.CALENDAR_READ);
            case NavigationMenuKey.TODOS:
                return createPermissionKeys(PermissionKeys.TODOS_READ);
            case NavigationMenuKey.APPROVAL_REQUEST:
                return createPermissionKeys(PermissionKeys.APPROVAL_REQUEST);
            case NavigationMenuKey.APPROVAL_INBOX:
                return createPermissionKeys(PermissionKeys.APPROVAL_APPROVE, PermissionKeys.APPROVAL_FINAL_APPROVE);
            case NavigationMenuKey.APPROVAL_HISTORY:
                return createPermissionKeys(
                    PermissionKeys.APPROVAL_HISTORY_READ,
                    PermissionKeys.APPROVAL_REQUEST,
                    PermissionKeys.APPROVAL_APPROVE,
                    PermissionKeys.APPROVAL_FINAL_APPROVE);
            case NavigationMenuKey.ADMIN_USERS:
                return createPermissionKeys(PermissionKeys.USERS_MANAGE);
            case NavigationMenuKey.ADMIN_TEAMS:
                return createPermissionKeys(PermissionKeys.ORGANIZATION_MANAGE);
            case NavigationMenuKey.ADMIN_POSITIONS:
                return createPermissionKeys(PermissionKeys.POSITIONS_MANAGE, PermissionKeys.PERMISSIONS_MANAGE);
            case NavigationMenuKey.ADMIN_APPROVAL_WORKFLOWS:
                return createPermissionKeys(PermissionKeys.APPROVAL_WORKFLOWS_MANAGE);
            case NavigationMenuKey.ADMIN_SETTINGS:
                return createPermissionKeys(PermissionKeys.SYSTEM_SETTINGS_MANAGE);
            case NavigationMenuKey.ADMIN_GOOGLE:
                return createPermissionKeys(PermissionKeys.GOOGLE_CONNECTION_MANAGE);
            case NavigationMenuKey.ADMIN_MENUS:
                return createPermissionKeys(PermissionKeys.MENUS_MANAGE);
            case NavigationMenuKey.ADMIN_CALENDAR_AUDIT:
                return createPermissionKeys(PermissionKeys.CALENDAR_AUDIT_READ);
            case NavigationMenuKey.UPLOAD:
                return createPermissionKeys(PermissionKeys.FILES_UPLOAD);
            case NavigationMenuKey.MATERIALS:
            case NavigationMenuKey.MANAGEMENT_FILES:
            case NavigationMenuKey.COMMON:
            case NavigationMenuKey.PUBLIC:
            case NavigationMenuKey.RESPONSIBLE_RESOURCES:
            case NavigationMenuKey.APPROVED_FILES:
            case NavigationMenuKey.WORK_GUIDELINES:
            case NavigationMenuKey.WORK_RESOURCES:
                return createPermissionKeys(PermissionKeys.FILES_READ);
            default:
                return createPermissionKeys(PermissionKeys.MENU_READ);
        }
    }

    private static IReadOnlyList<PermissionKey> createPermissionKeys(params string[] permissionKeyValues)
    {
        List<PermissionKey> permissionKeys = new List<PermissionKey>();
        foreach (string permissionKeyValue in permissionKeyValues)
        {
            permissionKeys.Add(new PermissionKey(permissionKeyValue));
        }

        return permissionKeys;
    }
}
