using Microsoft.EntityFrameworkCore;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService : IApprovalService
{
    private const int RELATED_HISTORY_CANDIDATE_COUNT = 100;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly IDriveBrowserService? mDriveBrowserServiceOrNull;
    private readonly IDriveFileNameConflictService? mDriveFileNameConflictServiceOrNull;
    private readonly IChangeVersionService? mChangeVersionServiceOrNull;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;

    public ApprovalService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPermissionService permissionService,
        IDriveService driveService,
        IOrganizationPermissionService organizationPermissionService,
        IDriveBrowserService? driveBrowserServiceOrNull = null,
        IChangeVersionService? changeVersionServiceOrNull = null,
        IDriveFileNameConflictService? driveFileNameConflictServiceOrNull = null)
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
        mOrganizationPermissionService = organizationPermissionService;
        mDriveBrowserServiceOrNull = driveBrowserServiceOrNull;
        mChangeVersionServiceOrNull = changeVersionServiceOrNull;
        mDriveFileNameConflictServiceOrNull = driveFileNameConflictServiceOrNull;
    }
}
