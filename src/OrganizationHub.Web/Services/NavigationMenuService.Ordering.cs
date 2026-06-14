using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class NavigationMenuService
{
    private IReadOnlyList<string> getMenuPermissionKeys(NavigationMenuItem menuItem)
    {
        switch (menuItem.MenuKey)
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

    private int getVisibleMenuGroupOrder(NavigationMenuItem menuItem)
    {
        switch (menuItem.MenuKey)
        {
            case "Dashboard":
                return 10;
            case "Calendar":
            case "Todos":
            case "WorkGuidelines":
            case "WorkResources":
                return 20;
            case "Materials":
            case "ManagementFiles":
            case "Common":
            case "Public":
            case "Upload":
            case "ResponsibleResources":
                return 30;
            case "ApprovalRequest":
            case "ApprovalInbox":
            case "ApprovalHistory":
            case "ApprovedFiles":
                return 40;
            default:
                break;
        }

        switch (menuItem.AccessLevel)
        {
            case EMenuAccessLevel.Authenticated:
                return 20;
            case EMenuAccessLevel.TeamMember:
                return 30;
            case EMenuAccessLevel.TeamLeader:
                return 30;
            case EMenuAccessLevel.ManagementManager:
                return 50;
            case EMenuAccessLevel.Management:
                return 50;
            case EMenuAccessLevel.Admin:
                return 50;
            default:
                return 90;
        }
    }
}
