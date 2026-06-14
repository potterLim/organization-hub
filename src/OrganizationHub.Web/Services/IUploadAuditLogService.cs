using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public interface IUploadAuditLogService
{
    Task RecordUploadAsync(
        UserSession userSession,
        DriveFolderReference targetFolder,
        DriveFileItem uploadedFile,
        CancellationToken cancellationToken);

    Task AttachUploadMetadataAsync(IReadOnlyList<DriveFileItem> files, CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsAsync(
        UserSession userSession,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForReadableRootsAsync(
        UserSession userSession,
        EDriveFolderKind folderKind,
        RecentItemLimit maximumCount,
        EManagementTeamFilter managementTeamFilter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForRootAsync(
        UserSession userSession,
        DriveFolderReference rootFolder,
        RecentItemLimit maximumCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadAuditLogItem>> GetRecentUploadsForFolderAsync(
        DriveFolderId folderId,
        TimeSpan maximumAge,
        CancellationToken cancellationToken);
}
