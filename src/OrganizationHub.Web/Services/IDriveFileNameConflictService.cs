using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IDriveFileNameConflictService
{
    Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(
        DriveFolderId folderId,
        DriveItemName fileName,
        CancellationToken cancellationToken);
}
