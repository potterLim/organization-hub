using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService : IApprovalService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly IDriveBrowserService? mDriveBrowserServiceOrNull;
    private readonly IChangeVersionService? mChangeVersionServiceOrNull;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public ApprovalService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPermissionService permissionService, IDriveService driveService, IDriveBrowserService? driveBrowserServiceOrNull = null, IChangeVersionService? changeVersionServiceOrNull = null)
        : this(dbContextFactory, permissionService, driveService, new OrganizationPermissionService(dbContextFactory), driveBrowserServiceOrNull, changeVersionServiceOrNull)
    {
    }

    public ApprovalService(IDbContextFactory<OrganizationHubDbContext> dbContextFactory, IPermissionService permissionService, IDriveService driveService, IOrganizationPermissionService organizationPermissionService, IDriveBrowserService? driveBrowserServiceOrNull = null, IChangeVersionService? changeVersionServiceOrNull = null)
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
        mOrganizationPermissionService = organizationPermissionService;
        mDriveBrowserServiceOrNull = driveBrowserServiceOrNull;
        mChangeVersionServiceOrNull = changeVersionServiceOrNull;
    }
}
