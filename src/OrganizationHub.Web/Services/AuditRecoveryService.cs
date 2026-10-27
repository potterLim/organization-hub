using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrganizationHub.Web.Data;

namespace OrganizationHub.Web.Services;

internal sealed class AuditRecoveryService
{
    private readonly UploadAuditRecoveryService mUploads;
    private readonly CalendarDeletionAuditRecoveryService mDeletions;

    public AuditRecoveryService(IDbContextFactory<OrganizationHubDbContext> factory, IGoogleApiCredentialFactory credentials, ILogger<AuditRecoveryService> logger, IDriveService? driveServiceOrNull = null)
    {
        mUploads = new UploadAuditRecoveryService(factory, driveServiceOrNull, logger);
        mDeletions = new CalendarDeletionAuditRecoveryService(factory, credentials, logger);
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await mUploads.RecoverAsync(cancellationToken);
        await mDeletions.RecoverAsync(cancellationToken);
    }
}
