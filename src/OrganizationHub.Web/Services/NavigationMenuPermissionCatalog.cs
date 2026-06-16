using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal static class NavigationMenuPermissionCatalog
{
    public static IReadOnlyList<string> GetRequiredPermissionKeys(string menuKey)
    {
        switch (menuKey)
        {
            case "Dashboard":
                return new List<string>();
            case "Calendar":
                return new[] { PermissionKeys.CALENDAR_READ };
            case "Todos":
                return new[] { PermissionKeys.TODOS_READ };
            case "ApprovalRequest":
                return new[] { PermissionKeys.APPROVAL_REQUEST };
            case "ApprovalInbox":
                return new[] { PermissionKeys.APPROVAL_APPROVE, PermissionKeys.APPROVAL_FINAL_APPROVE };
            case "ApprovalHistory":
                return new[] { PermissionKeys.APPROVAL_HISTORY_READ };
            case "AdminUsers":
                return new[] { PermissionKeys.USERS_MANAGE };
            case "AdminTeams":
                return new[] { PermissionKeys.ORGANIZATION_MANAGE };
            case "AdminPositions":
                return new[] { PermissionKeys.POSITIONS_MANAGE, PermissionKeys.PERMISSIONS_MANAGE };
            case "AdminApprovalWorkflows":
                return new[] { PermissionKeys.APPROVAL_WORKFLOWS_MANAGE };
            case "AdminSettings":
                return new[] { PermissionKeys.SYSTEM_SETTINGS_MANAGE };
            case "AdminGoogle":
                return new[] { PermissionKeys.GOOGLE_CONNECTION_MANAGE };
            case "AdminMenus":
                return new[] { PermissionKeys.MENUS_MANAGE };
            case "AdminCalendarAudit":
                return new[] { PermissionKeys.CALENDAR_AUDIT_READ };
            case "Upload":
                return new[] { PermissionKeys.FILES_UPLOAD };
            case "Materials":
            case "ManagementFiles":
            case "Common":
            case "Public":
            case "ResponsibleResources":
            case "ApprovedFiles":
            case "WorkGuidelines":
            case "WorkResources":
                return new[] { PermissionKeys.FILES_READ };
            default:
                return new[] { PermissionKeys.MENU_READ };
        }
    }

    public static string GetRequiredPermissionSummary(string menuKey)
    {
        IReadOnlyList<string> requiredPermissionKeys = GetRequiredPermissionKeys(menuKey);
        if (requiredPermissionKeys.Count == 0)
        {
            return "로그인한 사용자";
        }

        Dictionary<string, PermissionDefinition> definitionsByKey = PermissionCatalog.GetDefinitions()
            .ToDictionary(definition => definition.Key);
        List<string> permissionNames = new List<string>();
        foreach (string permissionKey in requiredPermissionKeys)
        {
            if (definitionsByKey.TryGetValue(permissionKey, out PermissionDefinition? definition))
            {
                permissionNames.Add(definition.DisplayName);
                continue;
            }

            permissionNames.Add(permissionKey);
        }

        return string.Join(" 또는 ", permissionNames);
    }
}
