using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class NavigationMenuService : INavigationMenuService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public NavigationMenuService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory)
        : this(dbContextFactory, new OrganizationPermissionService(dbContextFactory))
    {
    }

    public NavigationMenuService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IOrganizationPermissionService organizationPermissionService)
    {
        mDbContextFactory = dbContextFactory;
        mOrganizationPermissionService = organizationPermissionService;
    }
}
