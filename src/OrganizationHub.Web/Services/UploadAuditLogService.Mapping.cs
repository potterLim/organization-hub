using System.Collections.Generic;
using OrganizationHub.Web.Data.Entities;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Services;

public sealed partial class UploadAuditLogService
{
    private IReadOnlyList<UploadAuditLogItem> createItems(
        IReadOnlyList<UploadAuditLog> auditLogs,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById)
    {
        List<UploadAuditLogItem> items = new List<UploadAuditLogItem>();
        HashSet<string> addedFileIds = new HashSet<string>();
        foreach (UploadAuditLog auditLog in auditLogs)
        {
            if (shouldSkipDuplicateRecentFile(auditLog, addedFileIds))
            {
                continue;
            }

            items.Add(createItem(auditLog, uploaderUsersById));
        }

        return items;
    }

    private static bool shouldSkipDuplicateRecentFile(
        UploadAuditLog auditLog,
        HashSet<string> addedFileIds)
    {
        if (string.IsNullOrWhiteSpace(auditLog.FileId))
        {
            return false;
        }

        return addedFileIds.Add(auditLog.FileId) == false;
    }

    private UploadAuditLogItem createItem(
        UploadAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById)
    {
        return createItem(auditLog, null, uploaderUsersById);
    }

    private UploadAuditLogItem createItem(
        UploadAuditLog auditLog,
        DriveFileMetadata? fileMetadataOrNull,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById)
    {
        UploadAuditLogItem item = new UploadAuditLogItem();
        item.FileId = auditLog.FileId;
        item.FileName = fileMetadataOrNull == null || string.IsNullOrWhiteSpace(fileMetadataOrNull.Name) ? auditLog.FileName : fileMetadataOrNull.Name;
        item.FolderDisplayName = auditLog.FolderDisplayName;
        item.FolderKind = auditLog.FolderKind;
        item.UploaderDisplayName = auditLog.UploaderDisplayName;
        item.UploaderTeamName = getUploaderTeamName(auditLog, uploaderUsersById);
        item.UploaderPositionName = auditLog.UploaderPositionName;
        item.TeamName = auditLog.TeamName;
        item.UploadedAt = auditLog.UploadedAt;
        return item;
    }

    private static string getUploaderTeamName(
        UploadAuditLog auditLog,
        IReadOnlyDictionary<int, UserAccount> uploaderUsersById)
    {
        if (string.IsNullOrWhiteSpace(auditLog.UploaderTeamName) == false)
        {
            return auditLog.UploaderTeamName.Trim();
        }

        UserAccount? uploaderUserOrNull;
        if (uploaderUsersById.TryGetValue(auditLog.UploaderUserId, out uploaderUserOrNull) == false)
        {
            return string.Empty;
        }

        if (uploaderUserOrNull.Team == null)
        {
            return string.Empty;
        }

        return uploaderUserOrNull.Team.DisplayName;
    }
}
