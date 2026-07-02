using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

internal sealed partial class NavigationMenuService : INavigationMenuService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public NavigationMenuService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IOrganizationPermissionService organizationPermissionService
    )
    {
        mDbContextFactory = dbContextFactory;
        mOrganizationPermissionService = organizationPermissionService;
    }
}
