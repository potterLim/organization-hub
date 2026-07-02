using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal sealed class DriveFileNameConflictService : IDriveFileNameConflictService
{
    private static readonly TimeSpan RECENT_UPLOAD_CONSISTENCY_WINDOW = TimeSpan.FromMinutes(10);

    private readonly IDriveService mDriveService;
    private readonly IUploadAuditLogService mUploadAuditLogService;

    public DriveFileNameConflictService(
        IDriveService driveService,
        IUploadAuditLogService uploadAuditLogService)
    {
        mDriveService = driveService;
        mUploadAuditLogService = uploadAuditLogService;
    }

    public async Task<DriveFileNameConflictCheckResult> CheckFileNameConflictAsync(
        DriveFolderId folderId,
        DriveItemName fileName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveFileItem> driveItems = await mDriveService.ListFilesAsync(folderId, cancellationToken);
        IReadOnlyList<UploadAuditLogItem> recentUploads = await mUploadAuditLogService.GetRecentUploadsForFolderAsync(
            folderId,
            RECENT_UPLOAD_CONSISTENCY_WINDOW,
            cancellationToken);

        List<DriveFileItem> knownItems = mergeKnownItems(driveItems, recentUploads);
        List<DriveFileItem> conflictingFiles = findConflictingFiles(knownItems, fileName);
        DriveItemName suggestedNewFileName = DriveFileNamePolicy.CreateAvailableFileName(fileName, knownItems);
        return new DriveFileNameConflictCheckResult(fileName, suggestedNewFileName, conflictingFiles);
    }

    private static List<DriveFileItem> mergeKnownItems(
        IReadOnlyList<DriveFileItem> driveItems,
        IReadOnlyList<UploadAuditLogItem> recentUploads)
    {
        List<DriveFileItem> knownItems = driveItems.ToList();
        HashSet<DriveItemId> knownItemIds = new HashSet<DriveItemId>(knownItems.Select(item => DriveItemId.CreateOptional(item.Id)));
        foreach (UploadAuditLogItem recentUpload in recentUploads)
        {
            DriveItemId recentUploadFileId = DriveItemId.CreateOptional(recentUpload.FileId);
            if (knownItemIds.Contains(recentUploadFileId))
            {
                continue;
            }

            knownItems.Add(RecentUploadedFileItemMapper.CreateDriveFileItem(recentUpload));
            knownItemIds.Add(recentUploadFileId);
        }

        return knownItems;
    }

    private static List<DriveFileItem> findConflictingFiles(
        IReadOnlyList<DriveFileItem> knownItems,
        DriveItemName fileName)
    {
        string requestedFileName = fileName.GetRequiredValue();
        List<DriveFileItem> conflictingFiles = new List<DriveFileItem>();
        foreach (DriveFileItem item in knownItems)
        {
            if (item.IsFolder)
            {
                continue;
            }

            if (string.Equals(item.Name, requestedFileName, StringComparison.OrdinalIgnoreCase))
            {
                conflictingFiles.Add(item);
            }
        }

        return conflictingFiles;
    }
}
