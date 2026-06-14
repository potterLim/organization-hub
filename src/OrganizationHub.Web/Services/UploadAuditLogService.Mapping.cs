using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    private IReadOnlyList<UploadAuditLogItem> createItems(IReadOnlyList<UploadAuditLog> auditLogs)
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        foreach (UploadAuditLog auditLog in auditLogs)
        {
            items.Add(createItem(auditLog));
        }

        return items;
    }

    private UploadAuditLogItem createItem(UploadAuditLog auditLog)
    {
        return createItem(auditLog, null);
    }

    private UploadAuditLogItem createItem(UploadAuditLog auditLog, DriveFileMetadata? fileMetadataOrNull)
    {
        UploadAuditLogItem item = new UploadAuditLogItem();
        item.FileID = auditLog.FileID;
        item.FileName = fileMetadataOrNull == null || string.IsNullOrWhiteSpace(fileMetadataOrNull.Name) ? auditLog.FileName : fileMetadataOrNull.Name;
        item.FolderDisplayName = auditLog.FolderDisplayName;
        item.FolderKind = auditLog.FolderKind;
        item.UploaderDisplayName = auditLog.UploaderDisplayName;
        item.TeamName = auditLog.TeamName;
        item.UploadedAt = auditLog.UploadedAt;
        return item;
    }
}
