using System;
using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

internal static class RecentUploadedFileItemMapper
{
    public static List<DriveFileItem> CreateDriveFileItems(
        IReadOnlyList<UploadAuditLogItem> recentUploads,
        RecentItemLimit maximumCount
    )
    {
        List<DriveFileItem> files = new List<DriveFileItem>();
        foreach (UploadAuditLogItem recentUpload in recentUploads)
        {
            files.Add(createDriveFileItem(recentUpload));
            if (files.Count >= maximumCount.GetRequiredValue())
            {
                break;
            }
        }

        return files;
    }

    public static DriveFileItem CreateDriveFileItem(UploadAuditLogItem recentUpload)
    {
        return createDriveFileItem(recentUpload);
    }

    public static List<DriveFileItem> CreateDriveFileItems(
        IReadOnlyList<UploadAuditLogItem> snapshotUploads,
        IReadOnlyList<UploadAuditLogItem> verifiedUploads,
        RecentItemLimit maximumCount
    )
    {
        Dictionary<string, UploadAuditLogItem> verifiedUploadsByFileId = verifiedUploads
            .GroupBy(upload => upload.FileId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<DriveFileItem> files = new List<DriveFileItem>();
        foreach (UploadAuditLogItem snapshotUpload in snapshotUploads)
        {
            UploadAuditLogItem? verifiedUploadOrNull;
            if (verifiedUploadsByFileId.TryGetValue(snapshotUpload.FileId, out verifiedUploadOrNull) == false)
            {
                continue;
            }

            files.Add(createDriveFileItem(verifiedUploadOrNull));
            if (files.Count >= maximumCount.GetRequiredValue())
            {
                break;
            }
        }

        return files;
    }

    public static DriveFileItem CreateDriveFileItem(UploadAuditLogItem recentUpload, DriveFolderReference folder)
    {
        DriveFileItem file = createDriveFileItem(recentUpload);
        file.TeamKey = folder.TeamKey.GetOptionalValue();
        file.TeamName = string.IsNullOrWhiteSpace(recentUpload.TeamName) ? folder.TeamName : recentUpload.TeamName;
        if (string.IsNullOrWhiteSpace(file.TeamName))
        {
            file.TeamName = folder.DisplayName;
        }

        file.FolderDisplayName = folder.DisplayName;
        return file;
    }

    private static DriveFileItem createDriveFileItem(UploadAuditLogItem recentUpload)
    {
        DriveFileItem file = new DriveFileItem();
        file.Id = recentUpload.FileId;
        file.Name = recentUpload.FileName;
        file.MimeType = inferMimeTypeFromFileName(recentUpload.FileName);
        file.CreatedTime = recentUpload.UploadedAt;
        file.ModifiedTime = recentUpload.UploadedAt;
        file.IsFolder = false;
        file.TeamName = recentUpload.TeamName;
        file.FolderDisplayName = recentUpload.FolderDisplayName;
        file.UploadedByDisplayName = recentUpload.UploaderDisplayName;
        file.UploadedByTeamName = recentUpload.UploaderTeamName;
        file.UploadedByPositionName = recentUpload.UploaderPositionName;
        file.UploadedAt = recentUpload.UploadedAt;
        file.CanPreviewInSite = file.MimeType == GoogleMimeTypes.PDF;
        return file;
    }

    private static string inferMimeTypeFromFileName(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleMimeTypes.PDF;
        }

        return string.Empty;
    }
}
