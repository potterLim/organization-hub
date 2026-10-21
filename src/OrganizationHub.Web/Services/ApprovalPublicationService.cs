using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

public sealed partial class ApprovalPublicationService : IApprovalPublicationService
{
    private readonly IDbContextFactory<OrganizationHubDbContext> mDbContextFactory;
    private readonly IDriveService mDriveService;
    private readonly IOrganizationPermissionService mOrganizationPermissionService;
    private readonly IDriveBrowserService? mDriveBrowserServiceOrNull;
    private readonly IChangeVersionService? mChangeVersionServiceOrNull;
    private readonly ILogger<ApprovalPublicationService> mLogger;

    public ApprovalPublicationService(
        IDbContextFactory<OrganizationHubDbContext> dbContextFactory,
        IDriveService driveService,
        IOrganizationPermissionService organizationPermissionService,
        IDriveBrowserService? driveBrowserServiceOrNull = null,
        IChangeVersionService? changeVersionServiceOrNull = null,
        ILogger<ApprovalPublicationService>? loggerOrNull = null)
    {
        mDbContextFactory = dbContextFactory;
        mDriveService = driveService;
        mOrganizationPermissionService = organizationPermissionService;
        mDriveBrowserServiceOrNull = driveBrowserServiceOrNull;
        mChangeVersionServiceOrNull = changeVersionServiceOrNull;
        mLogger = NullLogger<ApprovalPublicationService>.Instance;
        if (loggerOrNull != null)
        {
            mLogger = loggerOrNull;
        }
    }
}
