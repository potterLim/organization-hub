using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Domain;

namespace OrganizationHub.Web.Services;

public sealed partial class NavigationMenuService
{
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
