using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrganizationHub.Web.Configuration;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalService : IApprovalService, IApprovalPublicationService
{
    private const int RELATED_HISTORY_MAXIMUM_COUNT = 100;

    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IPermissionService mPermissionService;
    private readonly IDriveService mDriveService;
    private readonly IDriveBrowserService? mDriveBrowserServiceOrNull;
    private readonly IChangeVersionService? mChangeVersionServiceOrNull;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;
    private readonly ILogger<ApprovalService> mLogger;
    private readonly long mMaximumSnapshotBytes;

    public ApprovalService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IPermissionService permissionService,
        IDriveService driveService,
        IOrganizationPermissionService organizationPermissionService,
        IDriveBrowserService? driveBrowserServiceOrNull = null,
        IChangeVersionService? changeVersionServiceOrNull = null,
        ILogger<ApprovalService>? loggerOrNull = null,
        IOptions<OrganizationHubOptions>? optionsOrNull = null)
    {
        mDbContextFactory = dbContextFactory;
        mPermissionService = permissionService;
        mDriveService = driveService;
        mOrganizationPermissionService = organizationPermissionService;
        mDriveBrowserServiceOrNull = driveBrowserServiceOrNull;
        mChangeVersionServiceOrNull = changeVersionServiceOrNull;
        mLogger = NullLogger<ApprovalService>.Instance;
        if (loggerOrNull != null)
        {
            mLogger = loggerOrNull;
        }

        mMaximumSnapshotBytes = OrganizationHub.Web.Domain.OrganizationHubFileLimits.DEFAULT_MAXIMUM_UPLOAD_BYTES;
        if (optionsOrNull != null)
        {
            mMaximumSnapshotBytes = optionsOrNull.Value.MaximumUploadBytes;
        }
    }
}
