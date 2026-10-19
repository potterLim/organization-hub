using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class DashboardAccessService
{
    private readonly INavigationMenuService mNavigationMenuService;

    public DashboardAccessService(INavigationMenuService navigationMenuService)
    {
        mNavigationMenuService = navigationMenuService;
    }

    public async Task<DashboardAccess> GetAccessAsync(UserSession userSession, CancellationToken cancellationToken)
    {
        return new DashboardAccess
        {
            CanOpenInbox = await mNavigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/approvals/inbox"), cancellationToken),
            CanOpenHistory = await mNavigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/approvals/history"), cancellationToken),
            CanOpenTodos = await mNavigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/todos"), cancellationToken),
            CanOpenCalendar = await mNavigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/calendar"), cancellationToken),
            CanOpenMaterials = await mNavigationMenuService.CanAccessPathAsync(userSession, NavigationPath.CreateOptional("/materials"), cancellationToken)
        };
    }
}
